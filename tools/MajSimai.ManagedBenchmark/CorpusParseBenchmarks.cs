using System;
using BenchmarkDotNet.Attributes;
using MajSimai.Logging;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// Measures parsing every sample file: one operation parses the whole corpus, as one run of the native benchmarks in
/// tools/NativeBenchmark does. An operation takes seconds on the full sample set, so pass
/// <c>--strategy Monitoring</c> with small <c>--warmupCount</c> and <c>--iterationCount</c> values, as
/// tools/benchmark-native.py does, unless you want BenchmarkDotNet to choose the counts.
/// </summary>
[MemoryDiagnoser]
public class CorpusParseBenchmarks
{
    /// <summary>
    /// The files to parse.
    /// </summary>
    private BenchmarkCorpus _corpus = null!;

    /// <summary>
    /// Reads the sample files named by the environment and installs a logger that writes nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the environment names no sample files.</exception>
    [GlobalSetup]
    public void GlobalSetup()
    {
        SimaiLogger.Current = DiscardingSimaiLogger.Instance;
        Setup(BenchmarkCorpus.FromEnvironment(Environment.GetEnvironmentVariable));
        Console.WriteLine($"// Corpus: {_corpus.Count} files, {_corpus.TotalBytes} bytes");
    }

    /// <summary>
    /// Parses every file with <see cref="SimaiParser.Parse(System.IO.Stream, SimaiParserOptions?)"/> in lenient mode,
    /// the way the native entry point does.
    /// </summary>
    /// <returns>The notes of every parsed file, a checksum of the work done.</returns>
    [Benchmark(Baseline = true)]
    public long Parse()
    {
        var notes = 0L;
        for (var index = 0; index < _corpus.Count; index++)
        {
            if (ManagedParser.Parse(_corpus.GetFile(index), out var fileNotes))
            {
                notes += fileNotes;
            }
        }
        return notes;
    }

    /// <summary>
    /// Parses every file through the native entry points, into native memory, and frees each result.
    /// </summary>
    /// <returns>The number of files the entry point parsed.</returns>
    [Benchmark]
    public int ParseToNative()
    {
        var parsed = 0;
        for (var index = 0; index < _corpus.Count; index++)
        {
            if (NativeAbiParser.Parse(_corpus.GetFile(index)))
            {
                parsed++;
            }
        }
        return parsed;
    }

    /// <summary>
    /// Uses a corpus instead of the one named by the environment.
    /// </summary>
    /// <param name="corpus">The sample files.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="corpus"/> is <see langword="null"/>.</exception>
    internal void Setup(BenchmarkCorpus corpus)
    {
        _corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
    }
}
