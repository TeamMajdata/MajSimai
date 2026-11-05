using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MajSimai.Tests.Equivalence;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Compiles the generated C++ wrapper with a real C++ compiler and, when a package of a native build is given, links
/// it with CMake, runs it and compares its results with the C# parser.
/// </summary>
/// <remarks>
/// The tests need a C++17 compiler: the one in the <c>CXX</c> environment variable, or <c>c++</c>, <c>g++</c> or
/// <c>clang++</c> on the path. The end-to-end test also needs CMake and <see cref="PackageVariable"/>, a package made by
/// <c>python tools/build-cpp-package.py --kind shared|static</c>.
/// </remarks>
public sealed class CppBindingNativeTests : IDisposable
{
    /// <summary>
    /// The environment variable that holds the folder of a package made by <c>tools/build-cpp-package.py</c>, absolute
    /// or relative to the repository.
    /// </summary>
    public const string PackageVariable = "MAJSIMAI_CPP_PACKAGE";

    /// <summary>
    /// The number of sample files the end-to-end test runs when the sample directory is set.
    /// </summary>
    private const int SampleCount = 200;

    /// <summary>
    /// A temporary directory for the compiled program and its inputs, deleted after each test.
    /// </summary>
    private readonly string _work = Path.Combine(Path.GetTempPath(), "MajSimai.CppBinding.Tests." + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets the path of the C++ test program.
    /// </summary>
    private static string TestProgram => Path.GetFullPath(Path.Combine(CppBindingGeneratorTests.CheckedInWrapper, "..", "..", "Tests", "MajSimai.Tests", "CppGen", "Native", "BindingTest.cpp"));

    /// <summary>
    /// Gets the include directory of the wrapper.
    /// </summary>
    private static string IncludeDirectory => Path.Combine(CppBindingGeneratorTests.CheckedInWrapper, "include");

    /// <summary>
    /// Deletes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    /// <summary>
    /// Wrapper: compiles as strict C++17 and C++20, which also checks every struct layout.
    /// </summary>
    /// <param name="standard">The C++ standard.</param>
    [Theory]
    [InlineData("c++17")]
    [InlineData("c++20")]
    public void Wrapper_CompilesAsStrictCpp(string standard)
    {
        var compiler = FindCompiler();
        Assert.SkipWhen(compiler is null, "No C++ compiler found; set CXX to run this test.");

        var (exitCode, output) = RunProcess(compiler!, $"-std={standard} -Wall -Wextra -Wpedantic -Werror -fsyntax-only -I \"{IncludeDirectory}\" \"{TestProgram}\"");

        Assert.True(exitCode == 0, output);
    }

    /// <summary>
    /// Unsafe ownership: code that would read freed native memory or free it twice does not compile.
    /// </summary>
    /// <param name="statement">A statement that misuses the owner of a parse result.</param>
    /// <param name="expectedError">A word of the compiler error, the same for GCC and Clang.</param>
    [Theory]
    [InlineData("auto view = MajSimai::SimaiParser::ParseAsView(\"x\").GetFile();", "deleted")]
    [InlineData("MajSimai::SimaiFileView view = MajSimai::SimaiParser::ParseAsView(\"x\");", "conversion")]
    [InlineData("auto parsed = MajSimai::SimaiParser::ParseAsView(\"x\"); auto copy = parsed;", "deleted")]
    [InlineData("MajSimai::Core::UnmanagedSimaiParseResult result{}; MajSimai::ParsedSimaiFile parsed(result);", "private")]
    public void UnsafeOwnership_DoesNotCompile(string statement, string expectedError)
    {
        var compiler = FindCompiler();
        Assert.SkipWhen(compiler is null, "No C++ compiler found; set CXX to run this test.");
        Directory.CreateDirectory(_work);
        var source = Path.Combine(_work, "misuse.cpp");
        File.WriteAllText(source, $"#include \"MajSimai/MajSimai.h\"\nint main()\n{{\n    {statement}\n    return 0;\n}}\n");

        var (exitCode, output) = RunProcess(compiler!, $"-std=c++17 -fsyntax-only -I \"{IncludeDirectory}\" \"{source}\"");

        Assert.NotEqual(0, exitCode);
        Assert.Contains(expectedError, output);
    }

    /// <summary>
    /// Package: gives the same results as the C# parser in lenient and strict mode.
    /// </summary>
    [Fact]
    public void Package_GivesTheSameResultsAsTheCSharpParser()
    {
        var cmake = FindProgram("cmake");
        var package = Environment.GetEnvironmentVariable(PackageVariable);
        Assert.SkipWhen(cmake is null, "CMake was not found on the path.");
        Assert.SkipWhen(string.IsNullOrEmpty(package), $"Set {PackageVariable} to a package made by tools/build-cpp-package.py to run this test.");
        var repository = Path.GetFullPath(Path.Combine(CppBindingGeneratorTests.CheckedInWrapper, "..", ".."));
        var program = BuildTestProgram(cmake!, Path.GetFullPath(package!, repository));
        var files = WriteInputs();
        var inputs = files.Select(f => (Path: f, Strict: false)).Concat(files.Select(f => (Path: f, Strict: true))).ToArray();
        var dump = Path.Combine(_work, "dump.txt");
        var arguments = new StringBuilder($"\"{dump}\"");
        foreach (var strict in new[] { false, true })
        {
            arguments.Append(strict ? " --strict" : " --lenient");
            foreach (var file in files)
            {
                arguments.Append(file is null ? " --empty" : $" \"{file}\"");
            }
        }

        var (exitCode, output) = RunProcess(program, arguments.ToString());

        Assert.True(exitCode == 0, output);
        using var reader = new StreamReader(dump, Encoding.UTF8);
        foreach (var (path, strict) in inputs)
        {
            var expected = Expect(path, strict);
            var actual = new char[expected.Length];
            var read = reader.ReadBlock(actual, 0, actual.Length);
            Assert.True(expected == new string(actual, 0, read), $"The C++ result differs for {path ?? "--empty"} (strict: {strict}):\n{new string(actual, 0, read)}\nExpected:\n{expected}");
        }
        Assert.Equal(-1, reader.Peek());
    }

    /// <summary>
    /// Builds <c>Native/BindingTest.cpp</c> with CMake against a package, the way a user project adds it.
    /// </summary>
    /// <param name="cmake">The CMake command.</param>
    /// <param name="package">The folder of the package.</param>
    /// <returns>The path of the built program.</returns>
    private string BuildTestProgram(string cmake, string package)
    {
        var project = Path.Combine(_work, "project");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "CMakeLists.txt"), $$"""
            cmake_minimum_required(VERSION 3.15)
            project(BindingTest LANGUAGES CXX)
            add_subdirectory("{{package.Replace('\\', '/')}}" majsimai)
            add_executable(BindingTest "{{TestProgram.Replace('\\', '/')}}")
            target_link_libraries(BindingTest PRIVATE MajSimai::MajSimaiCpp)
            get_target_property(MAJSIMAI_NATIVE_TYPE MajSimai::Native TYPE)
            if(WIN32 AND MAJSIMAI_NATIVE_TYPE STREQUAL "SHARED_LIBRARY")
                add_custom_command(TARGET BindingTest POST_BUILD
                    COMMAND "${CMAKE_COMMAND}" -E copy_if_different "$<TARGET_FILE:MajSimai::Native>" "$<TARGET_FILE_DIR:BindingTest>")
            endif()
            """);
        var build = Path.Combine(project, "build");
        var (configureExit, configureOutput) = RunProcess(cmake, $"-S \"{project}\" -B \"{build}\" -DCMAKE_BUILD_TYPE=Release");
        Assert.True(configureExit == 0, configureOutput);
        var (buildExit, buildOutput) = RunProcess(cmake, $"--build \"{build}\" --config Release");
        Assert.True(buildExit == 0, buildOutput);
        var program = new[] { "BindingTest", "BindingTest.exe", Path.Combine("Release", "BindingTest.exe") }
                      .Select(p => Path.Combine(build, p))
                      .FirstOrDefault(File.Exists);
        Assert.True(program is not null, "CMake did not build BindingTest.");

        return program!;
    }

    /// <summary>
    /// Writes the inputs of the end-to-end test: handmade files that cover UTF-8, byte order marks, line breaks, strict
    /// errors and every note kind, then up to <see cref="SampleCount"/> sample files.
    /// </summary>
    /// <returns>The paths of the files, and <see langword="null"/> for the empty input.</returns>
    private List<string?> WriteInputs()
    {
        var inputs = new List<string?>();
        var handmade = new[]
        {
            "&title=星になれ\n&artist=アーティスト\n&des=d\n&first=1.25\n&wholebpm=150\n&lv_5=13+\n&inote_5=(120){4}1-3[8:1]*-5[8:1]/2,Ch[4:1],\r\nE1f,1bx,{8}(150)<HS*2>3h[2:1],5$,6@>2[4:1],7-3[8:1]b,1`2`3,\n",
            "﻿&title=BOM\n&inote_1=(60)1,2,3,4,\n&inote_2=(60){4}1/8,1<5[4:1]/2-6[4:1],\n",
            "&title=no charts\n",
            "&inote_3=(120)1,{4\n",
            "&inote_1=(120)1,\n&inote_4=(120)1/b/2,星,\n",
        };
        for (var i = 0; i < handmade.Length; i++)
        {
            var path = Path.Combine(_work, $"handmade{i}.txt");
            File.WriteAllText(path, handmade[i], new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            inputs.Add(path);
        }
        var bigEndian = Path.Combine(_work, "utf16.txt");
        File.WriteAllText(bigEndian, "&title=UTF-16 星\n&inote_1=(120)1,", new UnicodeEncoding(bigEndian: true, byteOrderMark: true));
        inputs.Add(bigEndian);
        inputs.Add(null);
        var samples = Environment.GetEnvironmentVariable(SampleEquivalenceTests.SamplesDirectoryVariable);
        if (!string.IsNullOrEmpty(samples) && Directory.Exists(samples))
        {
            inputs.AddRange(Directory.EnumerateFiles(samples, "maidata.txt", SearchOption.AllDirectories)
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .Take(SampleCount));
        }

        return inputs;
    }

    /// <summary>
    /// Writes what the C++ program must print for an input, from the C# parser: it reads the bytes of the file the way
    /// <c>MajSimai_ParseWithTolerance</c> does, as UTF-8 unless the file has a byte order mark.
    /// </summary>
    /// <param name="path">The path of the file, or <see langword="null"/> for the empty input.</param>
    /// <param name="strict">Whether the input is parsed in strict mode.</param>
    /// <returns>The expected records.</returns>
    private static string Expect(string? path, bool strict)
    {
        var header = $"input {(path is null ? "--empty" : "file")} {(strict ? "strict" : "lenient")}\n";
        if (path is null)
        {
            return header + NativeDump.WriteError(UnmanagedSimaiParseResult.CODE_INVALID_CONTENT, -1, -1, "The content is empty.", string.Empty);
        }
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));

            return header + NativeDump.Write(SimaiParser.Parse(stream, strict ? SimaiParserOptions.Strict : SimaiParserOptions.Lenient));
        }
        catch (InvalidSimaiMarkupException e)
        {
            var code = e is InvalidSimaiSyntaxException
                ? UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX
                : UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP;

            return header + NativeDump.WriteError(code, e.Line, e.Column, e.Message, e.Content);
        }
    }

    /// <summary>
    /// Finds a C++ compiler.
    /// </summary>
    /// <returns>The compiler command, or <see langword="null"/> when there is none.</returns>
    private static string? FindCompiler()
    {
        var configured = Environment.GetEnvironmentVariable("CXX");

        return string.IsNullOrEmpty(configured) ? FindProgram("c++", "g++", "clang++") : configured;
    }

    /// <summary>
    /// Finds the first of several programs on the path.
    /// </summary>
    /// <param name="names">The program names, in order of preference.</param>
    /// <returns>The full path of the program, or <see langword="null"/> when none is on the path.</returns>
    private static string? FindProgram(params string[] names)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", string.Empty } : new[] { string.Empty };
        foreach (var name in names)
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(directory, name + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Runs a program and waits for it.
    /// </summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The exit code, and the standard output followed by the standard error.</returns>
    private static (int ExitCode, string Output) RunProcess(string fileName, string arguments)
    {
        var start = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not finish in 5 minutes.");
        }

        return (process.ExitCode, output + error.Result);
    }
}
