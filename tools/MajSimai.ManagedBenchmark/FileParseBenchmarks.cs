using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using MajSimai.Logging;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// Measures parsing one sample file, picked by its size among the sample files: the median file, the file at the
/// 99th percentile and the largest file. Each operation parses the file once, so BenchmarkDotNet can run it many times
/// and report the time and the memory of one parse.
/// </summary>
[MemoryDiagnoser]
public class FileParseBenchmarks
{
    /// <summary>
    /// The sample of median size.
    /// </summary>
    public const string Median = "median";

    /// <summary>
    /// The sample at the 99th percentile of size.
    /// </summary>
    public const string Percentile99 = "p99";

    /// <summary>
    /// The largest sample.
    /// </summary>
    public const string Largest = "largest";

    /// <summary>
    /// The file to parse.
    /// </summary>
    private ArraySegment<byte> _file;

    /// <summary>
    /// Gets or sets which sample to parse: <see cref="Median"/>, <see cref="Percentile99"/> or <see cref="Largest"/>.
    /// </summary>
    [Params(Median, Percentile99, Largest)]
    public string Sample { get; set; } = Median;

    /// <summary>
    /// Reads the sample files named by the environment, picks the sample and installs a logger that writes nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the environment names no sample files.</exception>
    [GlobalSetup]
    public void GlobalSetup()
    {
        SimaiLogger.Current = DiscardingSimaiLogger.Instance;
        Setup(BenchmarkCorpus.FromEnvironment(Environment.GetEnvironmentVariable));
        Console.WriteLine($"// Sample {Sample}: {_file.Count} bytes");
    }

    /// <summary>
    /// Parses the sample with <see cref="SimaiParser.Parse(Stream, SimaiParserOptions?)"/> in lenient mode.
    /// </summary>
    /// <returns>The parsed file.</returns>
    [Benchmark(Baseline = true)]
    public SimaiFile Parse()
    {
        using var stream = new MemoryStream(_file.Array!, _file.Offset, _file.Count, writable: false);

        return SimaiParser.Parse(stream, SimaiParserOptions.Lenient);
    }

    /// <summary>
    /// Parses the sample through the native entry points, into native memory, and frees the result.
    /// </summary>
    /// <returns>Whether the sample was parsed.</returns>
    [Benchmark]
    public bool ParseToNative()
    {
        return NativeAbiParser.Parse(_file);
    }

    /// <summary>
    /// Picks the sample of <see cref="Sample"/> from a corpus.
    /// </summary>
    /// <param name="corpus">The sample files.</param>
    /// <exception cref="ArgumentException">Thrown when <see cref="Sample"/> is not a known sample.</exception>
    internal void Setup(BenchmarkCorpus corpus)
    {
        _file = SelectSample(corpus, Sample);
    }

    /// <summary>
    /// Picks a file by its size rank among the files of a corpus; files of the same size keep their order.
    /// </summary>
    /// <param name="corpus">The sample files.</param>
    /// <param name="sample"><see cref="Median"/>, <see cref="Percentile99"/> or <see cref="Largest"/>.</param>
    /// <returns>The bytes of the file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sample"/> is not a known sample.</exception>
    internal static ArraySegment<byte> SelectSample(BenchmarkCorpus corpus, string sample)
    {
        if (corpus is null)
        {
            throw new ArgumentNullException(nameof(corpus));
        }
        if (sample is null)
        {
            throw new ArgumentNullException(nameof(sample));
        }
        var fraction = sample switch
        {
            Median => 0.5,
            Percentile99 => 0.99,
            Largest => 1.0,
            _ => throw new ArgumentException($"Unknown sample '{sample}'; use {Median}, {Percentile99} or {Largest}.", nameof(sample)),
        };
        var bySize = Enumerable.Range(0, corpus.Count)
                               .OrderBy(index => corpus.GetFile(index).Count)
                               .ThenBy(index => index)
                               .ToArray();

        return corpus.GetFile(bySize[(int)Math.Round(fraction * (bySize.Length - 1))]);
    }
}
