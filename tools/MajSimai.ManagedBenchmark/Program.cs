using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Running;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// The command line of the benchmarks: the options of BenchmarkDotNet, such as
/// <c>--filter "*FileParseBenchmarks*" --runtimes net10.0 nativeaot10.0</c>. Without <c>--filter</c> every benchmark
/// runs instead of BenchmarkDotNet asking which. The sample files come from the environment, see
/// <see cref="BenchmarkCorpus.FromEnvironment"/>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The exit code when every benchmark ran.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// The exit code when a benchmark failed or did not pass the validation of BenchmarkDotNet.
    /// </summary>
    public const int Failure = 1;

    /// <summary>
    /// The exit code when the environment names no sample files.
    /// </summary>
    public const int UsageError = 2;

    /// <summary>
    /// The options of BenchmarkDotNet that print information instead of running benchmarks.
    /// </summary>
    private static readonly string[] s_informationOptions = { "--help", "-h", "-?", "--version", "--info", "--list" };

    /// <summary>
    /// The options of BenchmarkDotNet that pick the benchmarks to run.
    /// </summary>
    private static readonly string[] s_filterOptions = { "--filter", "-f" };

    /// <summary>
    /// Runs the benchmarks.
    /// </summary>
    /// <param name="args">The options of BenchmarkDotNet.</param>
    /// <returns>The exit code.</returns>
    public static int Main(string[] args)
    {
        return Run(args, Environment.GetEnvironmentVariable, Console.Error, RunBenchmarks);
    }

    /// <summary>
    /// Checks that the environment names the sample files, unless only information is asked for, and runs the
    /// benchmarks.
    /// </summary>
    /// <param name="args">The options of BenchmarkDotNet.</param>
    /// <param name="getVariable">Reads an environment variable.</param>
    /// <param name="error">Where to write errors.</param>
    /// <param name="runBenchmarks">Runs BenchmarkDotNet with the options and returns whether every benchmark ran.</param>
    /// <returns><see cref="Success"/>, <see cref="Failure"/> or <see cref="UsageError"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    internal static int Run(string[] args, Func<string, string?> getVariable, TextWriter error, Func<string[], bool> runBenchmarks)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }
        if (getVariable is null)
        {
            throw new ArgumentNullException(nameof(getVariable));
        }
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }
        if (runBenchmarks is null)
        {
            throw new ArgumentNullException(nameof(runBenchmarks));
        }
        if (!IsInformationRequest(args))
        {
            var problem = BenchmarkCorpus.GetEnvironmentProblem(getVariable);
            if (problem is not null)
            {
                error.WriteLine($"error: {problem}");
                return UsageError;
            }
        }
        return runBenchmarks(WithDefaultFilter(args)) ? Success : Failure;
    }

    /// <summary>
    /// Gets whether the options only ask BenchmarkDotNet for information, such as <c>--list</c> or <c>--help</c>.
    /// </summary>
    /// <param name="args">The options.</param>
    /// <returns><see langword="true"/> when one of the options prints information instead of running benchmarks.</returns>
    internal static bool IsInformationRequest(IReadOnlyList<string> args)
    {
        return args.Any(arg => s_informationOptions.Contains(arg, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds <c>--filter *</c> when the options pick no benchmark, so every benchmark runs instead of BenchmarkDotNet
    /// asking which, which a script cannot answer.
    /// </summary>
    /// <param name="args">The options.</param>
    /// <returns>The options, with <c>--filter *</c> first when they need it.</returns>
    internal static string[] WithDefaultFilter(string[] args)
    {
        var picksBenchmarks = args.Any(arg => s_filterOptions.Contains(arg, StringComparer.OrdinalIgnoreCase)
                                              || arg.StartsWith("--filter=", StringComparison.OrdinalIgnoreCase));
        if (picksBenchmarks || IsInformationRequest(args))
        {
            return args;
        }
        return new[] { "--filter", "*" }.Concat(args).ToArray();
    }

    /// <summary>
    /// Runs BenchmarkDotNet on the benchmarks of this assembly.
    /// </summary>
    /// <param name="args">The options of BenchmarkDotNet.</param>
    /// <returns><see langword="true"/> when every benchmark passed the validation and ran.</returns>
    private static bool RunBenchmarks(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args).ToArray();

        return summaries.All(summary => !summary.HasCriticalValidationErrors && summary.Reports.All(report => report.Success));
    }
}
