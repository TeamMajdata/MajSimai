using System;
using System.Collections.Generic;
using System.IO;

namespace MajSimai.CppGen;

/// <summary>
/// The command line of the generator.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The exit code when the files were written or are up to date.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// The exit code of <c>--check</c> when a file is missing, different or stale.
    /// </summary>
    public const int OutOfDate = 1;

    /// <summary>
    /// The exit code when the arguments are wrong or the ABI cannot be turned into C++.
    /// </summary>
    public const int Failure = 2;

    /// <summary>
    /// The directory of the wrapper, relative to the repository root.
    /// </summary>
    public const string DefaultOutput = "Bindings/Cpp";

    /// <summary>
    /// The usage text.
    /// </summary>
    private const string Usage = """
        Generates the C++ wrapper of MajSimai from its native ABI (Runtime/Unmanaged).

        Usage: dotnet run --project tools/MajSimai.CppGen -- [--output <directory>] [--check]

          --output <directory>  Where to write the wrapper. Default: Bindings/Cpp in the repository.
          --check               Write nothing; exit with 1 when a file is missing, different or stale.
        """;

    /// <summary>
    /// Runs the generator.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    private static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Runs the generator with the given output streams.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="output">Receives progress messages.</param>
    /// <param name="error">Receives error messages.</param>
    /// <returns><see cref="Success"/>, <see cref="OutOfDate"/> or <see cref="Failure"/>.</returns>
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? outputDirectory = null;
        var check = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output" when i + 1 < args.Length:
                    outputDirectory = args[++i];
                    break;
                case "--check":
                    check = true;
                    break;
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return Success;
                default:
                    error.WriteLine($"Error: unknown or incomplete argument '{args[i]}'.");
                    error.WriteLine(Usage);
                    return Failure;
            }
        }
        try
        {
            outputDirectory ??= FindDefaultOutput(Directory.GetCurrentDirectory());
            var files = CppBindingGenerator.Generate(CppBindingGenerator.ReadMajSimai());

            return check ? Check(outputDirectory, files, output, error) : Write(outputDirectory, files, output);
        }
        catch (CppGenException e)
        {
            error.WriteLine($"Error: {e.Message}");
            return Failure;
        }
    }

    /// <summary>
    /// Finds <see cref="DefaultOutput"/> in the repository that contains a directory.
    /// </summary>
    /// <param name="start">The directory to search from, usually the current directory.</param>
    /// <returns>The full path of the default output directory.</returns>
    /// <exception cref="CppGenException">Thrown when the directory is not inside the MajSimai repository.</exception>
    internal static string FindDefaultOutput(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MajSimai.csproj")))
            {
                return Path.Combine(directory.FullName, DefaultOutput.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        throw new CppGenException($"'{start}' is not inside the MajSimai repository; pass --output.");
    }

    /// <summary>
    /// Writes the files and reports what changed.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="files">The generated files.</param>
    /// <param name="output">Receives progress messages.</param>
    /// <returns><see cref="Success"/>.</returns>
    private static int Write(string root, IReadOnlyList<GeneratedFile> files, TextWriter output)
    {
        var changed = OutputDirectory.Write(root, files);
        foreach (var path in changed)
        {
            output.WriteLine($"Updated {path}");
        }
        output.WriteLine(changed.Count == 0
            ? $"The C++ wrapper in '{root}' is up to date."
            : $"Updated {changed.Count} file(s) of the C++ wrapper in '{root}'.");

        return Success;
    }

    /// <summary>
    /// Checks the files and reports what is out of date.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="files">The generated files.</param>
    /// <param name="output">Receives progress messages.</param>
    /// <param name="error">Receives the list of files that are out of date.</param>
    /// <returns><see cref="Success"/> or <see cref="OutOfDate"/>.</returns>
    private static int Check(string root, IReadOnlyList<GeneratedFile> files, TextWriter output, TextWriter error)
    {
        var outdated = OutputDirectory.Check(root, files);
        if (outdated.Count == 0)
        {
            output.WriteLine($"The C++ wrapper in '{root}' is up to date.");
            return Success;
        }
        foreach (var path in outdated)
        {
            error.WriteLine($"Out of date: {path}");
        }
        error.WriteLine("Run `dotnet run --project tools/MajSimai.CppGen` to update the C++ wrapper.");

        return OutOfDate;
    }
}
