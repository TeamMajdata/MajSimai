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
/// Compiles the generated C++ wrapper with a real C++ compiler and, when a native build of MajSimai is given, runs it
/// and compares its results with the C# parser.
/// </summary>
/// <remarks>
/// The tests need a C++17 compiler: the one in the <c>CXX</c> environment variable, or <c>c++</c>, <c>g++</c> or
/// <c>clang++</c> on the path. The end-to-end test also needs <see cref="NativeLibraryVariable"/>, the path of MajSimai
/// published with native AOT for this platform, for example
/// <c>dotnet publish MajSimai.csproj -c Release -f net10.0 -r linux-x64 -p:PublishAot=true -o out</c>.
/// </remarks>
public sealed class CppBindingNativeTests : IDisposable
{
    /// <summary>
    /// The environment variable that holds the path of the native MajSimai library.
    /// </summary>
    public const string NativeLibraryVariable = "MAJSIMAI_NATIVE_LIBRARY";

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
    private static string TestProgram => Path.Combine(CppBindingGeneratorTests.CheckedInWrapper, "..", "..", "Tests", "MajSimai.Tests", "CppGen", "Native", "BindingTest.cpp");

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
    /// Wrapper: gives the same results as the C# parser when linked with the native library.
    /// </summary>
    [Fact]
    public void Wrapper_GivesTheSameResultsAsTheCSharpParser()
    {
        var compiler = FindCompiler();
        var library = Environment.GetEnvironmentVariable(NativeLibraryVariable);
        Assert.SkipWhen(compiler is null, "No C++ compiler found; set CXX to run this test.");
        Assert.SkipWhen(string.IsNullOrEmpty(library), $"Set {NativeLibraryVariable} to MajSimai published with native AOT to run this test.");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The test links with GNU linker options, so it only runs on Linux.");
        Directory.CreateDirectory(_work);
        var program = Path.Combine(_work, "BindingTest");
        var libraryDirectory = Path.GetDirectoryName(Path.GetFullPath(library!))!;
        var (buildExit, buildOutput) = RunProcess(compiler!, $"-std=c++17 -O1 -I \"{IncludeDirectory}\" \"{TestProgram}\" -o \"{program}\" -L \"{libraryDirectory}\" -l:{Path.GetFileName(library)} -Wl,-rpath,\"{libraryDirectory}\"");
        Assert.True(buildExit == 0, buildOutput);

        var inputs = WriteInputs();
        var dump = Path.Combine(_work, "dump.txt");
        var (exitCode, output) = RunProcess(program, $"\"{dump}\" " + string.Join(" ", inputs.Select(i => i is null ? "--empty" : $"\"{i}\"")));

        Assert.True(exitCode == 0, output);
        using var reader = new StreamReader(dump, Encoding.UTF8);
        foreach (var input in inputs)
        {
            var expected = input is null
                ? "input --empty\n" + NativeDump.WriteError(UnmanagedSimaiParseResult.CODE_INVALID_CONTENT, "The content is empty.")
                : "input file\n" + NativeDump.Write(ParseFile(input));
            var actual = new char[expected.Length];
            var read = reader.ReadBlock(actual, 0, actual.Length);
            Assert.True(expected == new string(actual, 0, read), $"The C++ result differs for {input ?? "--empty"}:\n{new string(actual, 0, read)}\nExpected:\n{expected}");
        }
        Assert.Equal(-1, reader.Peek());
    }

    /// <summary>
    /// Writes the inputs of the end-to-end test: handmade files that cover UTF-8, byte order marks, line breaks and
    /// every note kind, then up to <see cref="SampleCount"/> sample files.
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
    /// Parses a file the way <c>MajSimai_Parse</c> does: from its bytes, as UTF-8 unless it has a byte order mark.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The parsed file.</returns>
    private static SimaiFile ParseFile(string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));

        return SimaiParser.Parse(stream);
    }

    /// <summary>
    /// Finds a C++ compiler.
    /// </summary>
    /// <returns>The compiler command, or <see langword="null"/> when there is none.</returns>
    private static string? FindCompiler()
    {
        var configured = Environment.GetEnvironmentVariable("CXX");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var name in new[] { "c++", "g++", "clang++" })
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(Path.Combine(directory, name)))
                {
                    return Path.Combine(directory, name);
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
