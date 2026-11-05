using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MajSimai.Tests.Equivalence;
using MajSimai.Tests.Legacy;
using Xunit;

namespace MajSimai.Tests.Benchmarks;

/// <summary>
/// Measures MajSimai 2.2.3 and the current parser on the sample files.
/// </summary>
/// <remarks>
/// Runs only when <c>MAJSIMAI_BENCHMARK</c> is <c>1</c> and <c>MAJSIMAI_SAMPLES_DIR</c> names the sample folder. Build in
/// Release for meaningful numbers: <c>dotnet run -c Release -- -class "*ParserBenchmarkTests"</c>.
/// </remarks>
[Collection(nameof(ParserBenchmarkTests))]
[CollectionDefinition(nameof(ParserBenchmarkTests), DisableParallelization = true)]
public sealed class ParserBenchmarkTests
{
    /// <summary>
    /// The number of measured runs.
    /// </summary>
    private const int Iterations = 5;

    /// <summary>
    /// Compare legacy and current parser.
    /// </summary>
    [Fact]
    public void CompareLegacyAndCurrentParser()
    {
        var directory = Environment.GetEnvironmentVariable(SampleEquivalenceTests.SamplesDirectoryVariable);
        if (Environment.GetEnvironmentVariable("MAJSIMAI_BENCHMARK") != "1" || string.IsNullOrEmpty(directory))
        {
            Assert.Skip("Set MAJSIMAI_BENCHMARK=1 and MAJSIMAI_SAMPLES_DIR to run the benchmark.");
        }
        var files = Directory.GetFiles(directory!, "maidata.txt", SearchOption.AllDirectories)
                             .Select(File.ReadAllBytes)
                             .ToArray();
        var largest = files.OrderByDescending(f => f.Length).First();
        var legacy = LegacyParser.Instance;
        var output = TestContext.Current.TestOutputHelper;

        output?.WriteLine($"Files: {files.Length}, total {files.Sum(f => (long)f.Length) / 1024 / 1024} MiB, largest {largest.Length / 1024} KiB");
        Report(output, "All files, 2.2.3", Measure(files, bytes => legacy.ParseRaw(bytes)));
        Report(output, "All files, 3.0  ", Measure(files, bytes => ParseCurrent(bytes)));
        var single = new[] { largest };
        Report(output, "Largest file x50, 2.2.3", Measure(Enumerable.Repeat(largest, 50).ToArray(), bytes => legacy.ParseRaw(bytes)));
        Report(output, "Largest file x50, 3.0  ", Measure(Enumerable.Repeat(largest, 50).ToArray(), bytes => ParseCurrent(bytes)));
    }

    /// <summary>
    /// Parses a file with the current parser, ignoring markup errors.
    /// </summary>
    /// <param name="bytes">The raw bytes of the file.</param>
    private static void ParseCurrent(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        try
        {
            SimaiParser.Parse(stream);
        }
        catch (InvalidSimaiMarkupException)
        {
        }
    }

    /// <summary>
    /// Measures parsing a set of files.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <param name="parse">The parse function.</param>
    /// <returns>The median and fastest time and the bytes allocated by the last run.</returns>
    private static (double MedianMs, double MinMs, long Bytes) Measure(byte[][] files, Action<byte[]> parse)
    {
        foreach (var file in files.Take(200))
        {
            parse(file);
        }
        var times = new double[Iterations];
        long allocated = 0;
        for (var i = 0; i < Iterations; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            foreach (var file in files)
            {
                parse(file);
            }
            watch.Stop();
            allocated = GC.GetTotalAllocatedBytes(true) - before;
            times[i] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);

        return (times[Iterations / 2], times[0], allocated);
    }

    /// <summary>
    /// Writes a measurement.
    /// </summary>
    /// <param name="output">The test output.</param>
    /// <param name="name">The name of the measurement.</param>
    /// <param name="result">The measurement.</param>
    private static void Report(ITestOutputHelper? output, string name, (double MedianMs, double MinMs, long Bytes) result)
    {
        output?.WriteLine($"{name}: median {result.MedianMs,9:F1} ms, min {result.MinMs,9:F1} ms, allocated {result.Bytes / 1024.0 / 1024.0,9:F1} MiB");
    }
}
