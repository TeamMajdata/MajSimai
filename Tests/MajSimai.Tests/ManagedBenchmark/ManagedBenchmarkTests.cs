using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using MajSimai.Logging;
using MajSimai.ManagedBenchmark;
using Xunit;

namespace MajSimai.Tests.ManagedBenchmark;

/// <summary>
/// Tests the BenchmarkDotNet benchmarks of tools/MajSimai.ManagedBenchmark: reading the sample files, the parse
/// functions, the benchmark classes and the command line. The benchmarks themselves are not run here.
/// </summary>
public sealed class ManagedBenchmarkTests : IDisposable
{
    /// <summary>
    /// A file with three notes in its first chart and one in its second.
    /// </summary>
    private const string FourNotes = "&title=Bench\n&inote_1=(120){4}1,2/3,\n&inote_2=(120){4}5,\n";

    /// <summary>
    /// A temporary directory, deleted after each test.
    /// </summary>
    private readonly string _work = Path.Combine(Path.GetTempPath(), "MajSimai.ManagedBenchmark.Tests." + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Creates the temporary directory.
    /// </summary>
    public ManagedBenchmarkTests()
    {
        Directory.CreateDirectory(_work);
    }

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
    /// Read: reads every file of a corpus file, including an empty one, and their total size.
    /// </summary>
    [Fact]
    public void Read_ReadsEveryFileAndTheTotalSize()
    {
        var files = new[] { Encoding.UTF8.GetBytes("abc"), Array.Empty<byte>(), Encoding.UTF8.GetBytes("&title=x") };

        var corpus = BenchmarkCorpus.Read(new MemoryStream(WriteCorpus(files)));

        AssertFiles(files, corpus);
    }

    /// <summary>
    /// Read: rejects a corpus file that is empty, has no file, names more files than it can hold or ends inside a file.
    /// </summary>
    /// <param name="bytes">The malformed corpus file, as hex.</param>
    [Theory]
    [InlineData("")]
    [InlineData("0100")]
    [InlineData("00000000")]
    [InlineData("02000000")]
    [InlineData("FFFFFFFF00000000")]
    [InlineData("020000000400000061626364")]
    [InlineData("0100000005000000616263")]
    public void Read_RejectsAMalformedCorpus(string bytes)
    {
        Assert.Throws<InvalidDataException>(() => BenchmarkCorpus.Read(new MemoryStream(Convert.FromHexString(bytes))));
    }

    /// <summary>
    /// FromFiles: copies the files into the corpus, and rejects null and empty lists.
    /// </summary>
    [Fact]
    public void FromFiles_CopiesTheFiles()
    {
        var files = new[] { Encoding.UTF8.GetBytes("first"), Encoding.UTF8.GetBytes("second") };

        var corpus = BenchmarkCorpus.FromFiles(files);
        files[0][0] = (byte)'X';

        Assert.Equal("first", Encoding.UTF8.GetString(corpus.GetFile(0)));
        Assert.Equal(11, corpus.TotalBytes);
        Assert.Throws<ArgumentNullException>(() => BenchmarkCorpus.FromFiles(null!));
        Assert.Throws<ArgumentNullException>(() => BenchmarkCorpus.FromFiles(new byte[][] { null! }));
        Assert.Throws<ArgumentException>(() => BenchmarkCorpus.FromFiles(Array.Empty<byte[]>()));
        Assert.Throws<ArgumentNullException>(() => BenchmarkCorpus.Read(null!));
    }

    /// <summary>
    /// FromDirectory: reads every maidata.txt under the folder in ordinal order of their paths, and nothing else.
    /// </summary>
    [Fact]
    public void FromDirectory_ReadsEveryMaidataInOrdinalOrder()
    {
        WriteSample("b", "second");
        WriteSample("a", "first");
        WriteSample(Path.Combine("c", "nested"), "third");
        File.WriteAllText(Path.Combine(_work, "other.txt"), "ignored");

        var corpus = BenchmarkCorpus.FromDirectory(_work);

        AssertFiles(new[] { "first", "second", "third" }.Select(Encoding.UTF8.GetBytes).ToArray(), corpus);
    }

    /// <summary>
    /// FromDirectory: rejects a null folder, a missing folder and a folder without samples.
    /// </summary>
    [Fact]
    public void FromDirectory_RejectsAFolderWithoutSamples()
    {
        Assert.Throws<ArgumentNullException>(() => BenchmarkCorpus.FromDirectory(null!));
        Assert.Throws<DirectoryNotFoundException>(() => BenchmarkCorpus.FromDirectory(Path.Combine(_work, "missing")));
        Assert.Throws<InvalidDataException>(() => BenchmarkCorpus.FromDirectory(_work));
    }

    /// <summary>
    /// FromEnvironment: prefers the corpus file of MAJSIMAI_BENCHMARK_CORPUS over the folder of MAJSIMAI_SAMPLES_DIR.
    /// </summary>
    [Fact]
    public void FromEnvironment_PrefersTheCorpusFileOverTheSampleFolder()
    {
        WriteSample("a", "from folder");
        var corpusFile = Path.Combine(_work, "corpus.bin");
        File.WriteAllBytes(corpusFile, WriteCorpus(new[] { Encoding.UTF8.GetBytes("from corpus") }));
        var both = Variables(corpusFile, _work);
        var folderOnly = Variables(null, _work);

        var fromCorpus = BenchmarkCorpus.FromEnvironment(both);
        var fromFolder = BenchmarkCorpus.FromEnvironment(folderOnly);

        Assert.Null(BenchmarkCorpus.GetEnvironmentProblem(both));
        Assert.Null(BenchmarkCorpus.GetEnvironmentProblem(folderOnly));
        Assert.Equal("from corpus", Encoding.UTF8.GetString(Assert.Single(Files(fromCorpus))));
        Assert.Equal("from folder", Encoding.UTF8.GetString(Assert.Single(Files(fromFolder))));
    }

    /// <summary>
    /// FromEnvironment: reports which variable to set, or which path is missing, and throws for them.
    /// </summary>
    [Fact]
    public void FromEnvironment_ReportsAMissingSource()
    {
        var missing = Path.Combine(_work, "missing");
        var cases = new[]
        {
            (Variables(null, null), BenchmarkCorpus.SamplesDirectoryVariable),
            (Variables(missing, null), BenchmarkCorpus.CorpusFileVariable),
            (Variables(null, missing), BenchmarkCorpus.SamplesDirectoryVariable),
        };

        foreach (var (getVariable, variable) in cases)
        {
            Assert.Contains(variable, BenchmarkCorpus.GetEnvironmentProblem(getVariable));
            Assert.Throws<InvalidOperationException>(() => BenchmarkCorpus.FromEnvironment(getVariable));
        }
        Assert.Throws<ArgumentNullException>(() => BenchmarkCorpus.GetEnvironmentProblem(null!));
    }

    /// <summary>
    /// ManagedParser.Parse: parses a file and counts the notes of all its charts, also inside a larger array.
    /// </summary>
    [Fact]
    public void ManagedParser_CountsTheNotesOfEveryChart()
    {
        var file = Encoding.UTF8.GetBytes(FourNotes);
        var padded = new byte[file.Length + 8];
        Array.Fill(padded, (byte)'1');
        file.CopyTo(padded, 4);

        var parsed = ManagedParser.Parse(new ArraySegment<byte>(file), out var notes);
        var parsedInside = ManagedParser.Parse(new ArraySegment<byte>(padded, 4, file.Length), out var notesInside);

        Assert.True(parsed);
        Assert.Equal(4, notes);
        Assert.True(parsedInside);
        Assert.Equal(4, notesInside);
    }

    /// <summary>
    /// ManagedParser.Parse and NativeAbiParser.Parse: reject empty content, as MajSimai_ParseWithTolerance does.
    /// </summary>
    [Fact]
    public void Parsers_RejectEmptyContent()
    {
        var managed = ManagedParser.Parse(ArraySegment<byte>.Empty, out var notes);
        var native = NativeAbiParser.Parse(ArraySegment<byte>.Empty);

        Assert.False(managed);
        Assert.Equal(0, notes);
        Assert.False(native);
    }

    /// <summary>
    /// NativeAbiParser.Parse: parses a file through the native entry points, also inside a larger array.
    /// </summary>
    [Fact]
    public void NativeAbiParser_ParsesThroughTheEntryPoints()
    {
        var file = Encoding.UTF8.GetBytes(FourNotes);
        var padded = new byte[file.Length + 2];
        file.CopyTo(padded, 1);

        Assert.True(NativeAbiParser.Parse(new ArraySegment<byte>(file)));
        Assert.True(NativeAbiParser.Parse(new ArraySegment<byte>(padded, 1, file.Length)));
    }

    /// <summary>
    /// SelectSample: picks the median, 99th percentile and largest file by size, keeping the order of equal sizes.
    /// </summary>
    [Fact]
    public void SelectSample_PicksFilesBySize()
    {
        // Sizes 5, 1, 3, 3, 9: by size the order is 1, 3 (index 2), 3 (index 3), 5, 9.
        var corpus = BenchmarkCorpus.FromFiles(new[] { 5, 1, 3, 3, 9 }.Select((size, index) => Enumerable.Repeat((byte)index, size).ToArray()).ToArray());

        var median = FileParseBenchmarks.SelectSample(corpus, FileParseBenchmarks.Median);
        var percentile99 = FileParseBenchmarks.SelectSample(corpus, FileParseBenchmarks.Percentile99);
        var largest = FileParseBenchmarks.SelectSample(corpus, FileParseBenchmarks.Largest);

        Assert.Equal(new byte[] { 3, 3, 3 }, median.ToArray());
        Assert.Equal(9, percentile99.Count);
        Assert.Equal(9, largest.Count);
        Assert.Throws<ArgumentException>(() => FileParseBenchmarks.SelectSample(corpus, "p50"));
        Assert.Throws<ArgumentNullException>(() => FileParseBenchmarks.SelectSample(null!, FileParseBenchmarks.Median));
        Assert.Throws<ArgumentNullException>(() => FileParseBenchmarks.SelectSample(corpus, null!));
    }

    /// <summary>
    /// FileParseBenchmarks: both benchmarks parse the picked sample.
    /// </summary>
    [Fact]
    public void FileParseBenchmarks_ParseTheSample()
    {
        var corpus = BenchmarkCorpus.FromFiles(new[] { Encoding.UTF8.GetBytes("&title=small\n"), Encoding.UTF8.GetBytes(FourNotes) });
        var benchmarks = new FileParseBenchmarks { Sample = FileParseBenchmarks.Largest };
        benchmarks.Setup(corpus);

        var parsed = benchmarks.Parse();

        Assert.Equal("Bench", parsed.Title);
        Assert.Equal(4, parsed.Charts.Sum(chart => chart.NoteTimings.ToArray().Sum(timing => timing.Notes.Length)));
        Assert.True(benchmarks.ParseToNative());
    }

    /// <summary>
    /// CorpusParseBenchmarks: both benchmarks parse every file and skip the empty one.
    /// </summary>
    [Fact]
    public void CorpusParseBenchmarks_ParseEveryFile()
    {
        var corpus = BenchmarkCorpus.FromFiles(new[] { Encoding.UTF8.GetBytes(FourNotes), Array.Empty<byte>(), Encoding.UTF8.GetBytes(FourNotes) });
        var benchmarks = new CorpusParseBenchmarks();
        benchmarks.Setup(corpus);

        Assert.Equal(8, benchmarks.Parse());
        Assert.Equal(2, benchmarks.ParseToNative());
        Assert.Throws<ArgumentNullException>(() => benchmarks.Setup(null!));
    }

    /// <summary>
    /// The benchmark classes pass the validation of BenchmarkDotNet and have the expected cases.
    /// </summary>
    /// <param name="type">The benchmark class.</param>
    /// <param name="cases">The number of benchmark cases: methods times parameter values.</param>
    [Theory]
    [InlineData(typeof(FileParseBenchmarks), 6)]
    [InlineData(typeof(CorpusParseBenchmarks), 2)]
    public void BenchmarkClasses_PassTheValidationOfBenchmarkDotNet(Type type, int cases)
    {
        var info = BenchmarkConverter.TypeToBenchmarks(type);
        var parameters = new ValidationParameters(info.BenchmarksCases, info.Config);

        var errors = info.Config.GetValidators().SelectMany(validator => validator.Validate(parameters)).Where(error => error.IsCritical).ToArray();

        Assert.Empty(errors.Select(error => error.Message));
        Assert.Equal(cases, info.BenchmarksCases.Length);
        Assert.Single(info.BenchmarksCases, benchmark => benchmark.Descriptor.Baseline && benchmark.Descriptor.WorkloadMethod.Name == "Parse"
                                                         && (cases == 2 || benchmark.Parameters["Sample"] is FileParseBenchmarks.Median));
    }

    /// <summary>
    /// DiscardingSimaiLogger: accepts the levels of the default logger, so the parser does the same checks, and drops
    /// messages without failing.
    /// </summary>
    [Fact]
    public void DiscardingSimaiLogger_AcceptsTheDefaultLevels()
    {
        var defaultLogger = new ConsoleSimaiLogger();

        DiscardingSimaiLogger.Instance.Log(SimaiLogLevel.Error, "dropped", new InvalidOperationException());

        foreach (var level in Enum.GetValues<SimaiLogLevel>())
        {
            Assert.Equal(defaultLogger.IsEnabled(level), DiscardingSimaiLogger.Instance.IsEnabled(level));
        }
        Assert.True(DiscardingSimaiLogger.Instance.IsEnabled(SimaiLogLevel.Warning));
    }

    /// <summary>
    /// Run: runs BenchmarkDotNet with every benchmark picked when no filter is given, and returns its outcome.
    /// </summary>
    [Fact]
    public void Run_RunsTheBenchmarksWithADefaultFilter()
    {
        var error = new StringWriter();
        var calls = new List<string[]>();
        var getVariable = Variables(null, _work);

        var succeeded = Program.Run(new[] { "--job", "short" }, getVariable, error, args => Record(calls, args, true));
        var filtered = Program.Run(new[] { "--filter", "*Corpus*" }, getVariable, error, args => Record(calls, args, true));
        var failed = Program.Run(Array.Empty<string>(), getVariable, error, args => Record(calls, args, false));

        Assert.Equal(Program.Success, succeeded);
        Assert.Equal(Program.Success, filtered);
        Assert.Equal(Program.Failure, failed);
        Assert.Equal(new[] { "--filter", "*", "--job", "short" }, calls[0]);
        Assert.Equal(new[] { "--filter", "*Corpus*" }, calls[1]);
        Assert.Equal(new[] { "--filter", "*" }, calls[2]);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// Run: refuses to run without sample files, but passes requests for information on.
    /// </summary>
    [Fact]
    public void Run_NeedsSampleFilesUnlessOnlyInformationIsAskedFor()
    {
        var error = new StringWriter();
        var calls = new List<string[]>();
        var nothing = Variables(null, null);

        var refused = Program.Run(new[] { "--filter", "*" }, nothing, error, args => Record(calls, args, true));
        var listed = Program.Run(new[] { "--list", "flat" }, nothing, new StringWriter(), args => Record(calls, args, true));

        Assert.Equal(Program.UsageError, refused);
        Assert.Contains(BenchmarkCorpus.SamplesDirectoryVariable, error.ToString());
        Assert.Equal(Program.Success, listed);
        Assert.Equal(new[] { "--list", "flat" }, Assert.Single(calls));
        Assert.Throws<ArgumentNullException>(() => Program.Run(null!, nothing, error, args => true));
        Assert.Throws<ArgumentNullException>(() => Program.Run(Array.Empty<string>(), null!, error, args => true));
        Assert.Throws<ArgumentNullException>(() => Program.Run(Array.Empty<string>(), nothing, null!, args => true));
        Assert.Throws<ArgumentNullException>(() => Program.Run(Array.Empty<string>(), nothing, error, null!));
    }

    /// <summary>
    /// WithDefaultFilter: keeps options that already pick benchmarks or only ask for information.
    /// </summary>
    /// <param name="arguments">The options, separated by spaces.</param>
    [Theory]
    [InlineData("--filter *File*")]
    [InlineData("-f *File*")]
    [InlineData("--filter=*File*")]
    [InlineData("--help")]
    [InlineData("--LIST tree")]
    [InlineData("--info")]
    public void WithDefaultFilter_KeepsOptionsThatPickBenchmarks(string arguments)
    {
        var args = arguments.Split(' ');

        Assert.Equal(args, Program.WithDefaultFilter(args));
    }

    /// <summary>
    /// Records the options BenchmarkDotNet would have been run with.
    /// </summary>
    /// <param name="calls">The recorded options.</param>
    /// <param name="args">The options.</param>
    /// <param name="outcome">The outcome to return.</param>
    /// <returns><paramref name="outcome"/>.</returns>
    private static bool Record(List<string[]> calls, string[] args, bool outcome)
    {
        calls.Add(args);
        return outcome;
    }

    /// <summary>
    /// Creates an environment with the two variables the benchmarks read.
    /// </summary>
    /// <param name="corpusFile">The value of MAJSIMAI_BENCHMARK_CORPUS, or <see langword="null"/>.</param>
    /// <param name="samples">The value of MAJSIMAI_SAMPLES_DIR, or <see langword="null"/>.</param>
    /// <returns>The function that reads a variable.</returns>
    private static Func<string, string?> Variables(string? corpusFile, string? samples)
    {
        return name => name switch
        {
            BenchmarkCorpus.CorpusFileVariable => corpusFile,
            BenchmarkCorpus.SamplesDirectoryVariable => samples,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the bytes of every file of a corpus.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <returns>The files.</returns>
    private static byte[][] Files(BenchmarkCorpus corpus)
    {
        return Enumerable.Range(0, corpus.Count).Select(index => corpus.GetFile(index).ToArray()).ToArray();
    }

    /// <summary>
    /// Checks that a corpus holds exactly some files.
    /// </summary>
    /// <param name="expected">The files.</param>
    /// <param name="corpus">The corpus.</param>
    private static void AssertFiles(IReadOnlyList<byte[]> expected, BenchmarkCorpus corpus)
    {
        Assert.Equal(expected, Files(corpus));
        Assert.Equal(expected.Sum(file => (long)file.Length), corpus.TotalBytes);
    }

    /// <summary>
    /// Writes a maidata.txt in a subfolder of the temporary directory.
    /// </summary>
    /// <param name="folder">The subfolder.</param>
    /// <param name="content">The content of the file.</param>
    private void WriteSample(string folder, string content)
    {
        var directory = Path.Combine(_work, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "maidata.txt"), content);
    }

    /// <summary>
    /// Writes files in the corpus file format of tools/benchmark-native.py.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <returns>The corpus file.</returns>
    private static byte[] WriteCorpus(IReadOnlyList<byte[]> files)
    {
        using var stream = new MemoryStream();
        var length = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)files.Count);
        stream.Write(length);
        foreach (var file in files)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)file.Length);
            stream.Write(length);
            stream.Write(file);
        }
        return stream.ToArray();
    }
}
