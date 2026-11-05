using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using MajSimai.Logging;
using MajSimai.Parsing;

namespace MajSimai;

/// <summary>
/// Parses simai files and charts, and writes simai files back to text.
/// </summary>
/// <remarks>
/// The seven charts of a file are parsed in parallel, and the notes of a long chart are read in parallel as well. In
/// lenient mode (the default) a chart that fails to parse does not fail the file: it is kept with its text but without
/// timing points, and the error is logged through <see cref="SimaiLogger"/>. In strict mode
/// (<see cref="SimaiParserOptions.Strict"/>) the first chart that fails makes the whole parse fail. Numbers are always
/// read with the invariant culture.
/// </remarks>
public static class SimaiParser
{
    /// <summary>
    /// The total length of chart text, in characters, from which the charts of a file are parsed in parallel. One
    /// thread parses about 20,000 characters per millisecond, so a shorter file is done before other threads would
    /// help. On the sample charts this keeps a file of median size (about 4,000 characters) on one thread and gave the
    /// shortest time over all files, with a third of the processor time of parsing every file in parallel.
    /// </summary>
    internal const int ParallelChartTextLength = 8 * 1024;

    #region Parse

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/>.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(ReadOnlySpan<char> content, string hash, SimaiParserOptions? options = null)
    {
        return Parse(ParseMetadata(content, hash, options), options);
    }

    /// <summary>
    /// Parses the charts of <paramref name="metadata"/> into a <see cref="SimaiFile"/>.
    /// </summary>
    /// <param name="metadata">The metadata read by <see cref="ParseMetadata(ReadOnlySpan{char}, string, SimaiParserOptions)"/>.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed file.</returns>
    public static SimaiFile Parse(SimaiMetadata metadata, SimaiParserOptions? options = null)
    {
        var charts = new SimaiChart[SimaiMetadata.ChartCount];
        var errors = new Exception?[charts.Length];
        if (ShouldParseChartsInParallel(metadata.Fumens))
        {
            Parallel.For(0, charts.Length, i => ParseChartForFile(metadata, i, options, charts, errors));
        }
        else
        {
            for (var i = 0; i < charts.Length; i++)
            {
                ParseChartForFile(metadata, i, options, charts, errors);
            }
        }
        ThrowFirst(errors);
        LogParsedFile(metadata, charts);

        return new SimaiFile(metadata, charts);
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 (or the encoding of its byte order mark) and parses it into a
    /// <see cref="SimaiFile"/>. The hash is the Base64 MD5 hash of the bytes.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(Stream contentStream, SimaiParserOptions? options = null)
    {
        return Parse(contentStream, Encoding.UTF8, options);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> (or the encoding of its byte order mark) and parses it
    /// into a <see cref="SimaiFile"/>. The hash is the Base64 MD5 hash of the bytes.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(Stream contentStream, Encoding encoding, SimaiParserOptions? options = null)
    {
        using var content = ContentDecoder.Decode(contentStream, encoding);

        return Parse(content.Span, content.Hash, options);
    }

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    public static Task<SimaiFile> ParseAsync(string content, string hash, SimaiParserOptions? options = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return ParseAsync(content.AsMemory(), hash, options);
    }

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text; it must not change until the task completes.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(ReadOnlyMemory<char> content, string hash, SimaiParserOptions? options = null)
    {
        var metadata = await ParseMetadataAsync(content, hash, options).ConfigureAwait(false);

        return await ParseAsync(metadata, options).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the charts of <paramref name="metadata"/> into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(SimaiMetadata metadata, SimaiParserOptions? options = null)
    {
        var tasks = StartChartTasks(metadata, options);
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception)
        {
            foreach (var task in tasks)
            {
                if (task.IsFaulted)
                {
                    ExceptionDispatchInfo.Capture(task.Exception!.InnerException!).Throw();
                }
            }
            throw;
        }
        var charts = new SimaiChart[tasks.Length];
        for (var i = 0; i < tasks.Length; i++)
        {
            charts[i] = tasks[i].Result;
        }
        LogParsedFile(metadata, charts);

        return new SimaiFile(metadata, charts);
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and parses it into a <see cref="SimaiFile"/> without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static Task<SimaiFile> ParseAsync(Stream contentStream, SimaiParserOptions? options = null)
    {
        return ParseAsync(contentStream, Encoding.UTF8, options);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and parses it into a <see cref="SimaiFile"/> without
    /// blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(Stream contentStream, Encoding encoding, SimaiParserOptions? options = null)
    {
        using var content = await ContentDecoder.DecodeAsync(contentStream, encoding).ConfigureAwait(false);

        return await ParseAsync(content.Memory, content.Hash, options).ConfigureAwait(false);
    }

    #endregion

    #region ParseMetadata

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/>. The notes are not parsed.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(ReadOnlySpan<char> content, string hash, SimaiParserOptions? options = null)
    {
        return MetadataReader.Read(content, hash, SyntaxReporter.For(options));
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and reads its fields into <see cref="SimaiMetadata"/>.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(Stream contentStream, SimaiParserOptions? options = null)
    {
        return ParseMetadata(contentStream, Encoding.UTF8, options);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and reads its fields into <see cref="SimaiMetadata"/>.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(Stream contentStream, Encoding encoding, SimaiParserOptions? options = null)
    {
        using var content = ContentDecoder.Decode(contentStream, encoding);

        return ParseMetadata(content.Span, content.Hash, options);
    }

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(string content, string hash, SimaiParserOptions? options = null)
    {
        return Task.Run(() => ParseMetadata(content.AsSpan(), hash, options));
    }

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text; it must not change until the task completes.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(ReadOnlyMemory<char> content, string hash, SimaiParserOptions? options = null)
    {
        return Task.Run(() => ParseMetadata(content.Span, hash, options));
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and reads its fields into <see cref="SimaiMetadata"/> without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream, SimaiParserOptions? options = null)
    {
        return ParseMetadataAsync(contentStream, Encoding.UTF8, options);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and reads its fields into <see cref="SimaiMetadata"/>
    /// without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static async Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream, Encoding encoding, SimaiParserOptions? options = null)
    {
        using var content = await ContentDecoder.DecodeAsync(contentStream, encoding).ConfigureAwait(false);

        return await ParseMetadataAsync(content.Memory, content.Hash, options).ConfigureAwait(false);
    }

    #endregion

    #region ParseChart

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> without a level or designer.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for other failures.</exception>
    public static SimaiChart ParseChart(ReadOnlySpan<char> fumen, SimaiParserOptions? options = null)
    {
        return ParseChart(string.Empty, string.Empty, fumen, options);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for other failures.</exception>
    public static SimaiChart ParseChart(string level, string designer, ReadOnlySpan<char> fumen, SimaiParserOptions? options = null)
    {
        return ChartParser.Parse(level, designer, fumen, null, SyntaxReporter.For(options), true);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/>, keeping the given string as <see cref="SimaiChart.Fumen"/>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <param name="allowParallel">Whether the notes of a long chart may be read on several threads.</param>
    /// <returns>The parsed chart.</returns>
    internal static SimaiChart ParseChart(string level, string designer, string fumen, SimaiParserOptions? options, bool allowParallel)
    {
        return ChartParser.Parse(level, designer, fumen.AsSpan(), fumen, SyntaxReporter.For(options), allowParallel);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string fumen, SimaiParserOptions? options = null)
    {
        return ParseChartAsync(string.Empty, string.Empty, fumen, options);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="fumen">The chart text; it must not change until the task completes.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(ReadOnlyMemory<char> fumen, SimaiParserOptions? options = null)
    {
        return ParseChartAsync(string.Empty, string.Empty, fumen, options);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text; it must not change until the task completes.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string level, string designer, ReadOnlyMemory<char> fumen, SimaiParserOptions? options = null)
    {
        return Task.Run(() => ParseChart(level, designer, fumen.Span, options));
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="options">The parser options; <see langword="null"/> uses <see cref="SimaiParserOptions.Lenient"/>.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string level, string designer, string fumen, SimaiParserOptions? options = null)
    {
        return Task.Run(() => ParseChart(level, designer, fumen ?? string.Empty, options, true));
    }

    #endregion

    #region Deparse

    /// <summary>
    /// Writes a simai file as text. Charts are written from <see cref="SimaiChart.Fumen"/>.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <returns>The simai text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="simaiFile"/> is <see langword="null"/>.</exception>
    public static string Deparse(SimaiFile simaiFile)
    {
        return SimaiDeparser.Deparse(simaiFile);
    }

    /// <summary>
    /// Writes a simai file to a stream with UTF-8. The stream is closed afterwards.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <param name="stream">The stream to write to.</param>
    public static void Deparse(SimaiFile simaiFile, Stream stream)
    {
        Deparse(simaiFile, stream, Encoding.UTF8);
    }

    /// <summary>
    /// Writes a simai file to a stream with <paramref name="encoding"/>. The stream is closed afterwards.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="encoding">The encoding of the text.</param>
    public static void Deparse(SimaiFile simaiFile, Stream stream, Encoding encoding)
    {
        var text = Deparse(simaiFile);
        using var writer = new StreamWriter(stream, encoding);
        writer.Write(text);
    }

    /// <summary>
    /// Writes a simai file as text on the thread pool.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <returns>A task that returns the simai text.</returns>
    public static Task<string> DeparseAsync(SimaiFile simaiFile)
    {
        return Task.Run(() => Deparse(simaiFile));
    }

    /// <summary>
    /// Writes a simai file to a stream with UTF-8 without blocking. The stream is closed afterwards.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <returns>A task that completes when the text is written.</returns>
    public static Task DeparseAsync(SimaiFile simaiFile, Stream stream)
    {
        return DeparseAsync(simaiFile, stream, Encoding.UTF8);
    }

    /// <summary>
    /// Writes a simai file to a stream with <paramref name="encoding"/> without blocking. The stream is closed
    /// afterwards.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <returns>A task that completes when the text is written.</returns>
    public static async Task DeparseAsync(SimaiFile simaiFile, Stream stream, Encoding encoding)
    {
        var text = await DeparseAsync(simaiFile).ConfigureAwait(false);
        using var writer = new StreamWriter(stream, encoding);
        await writer.WriteAsync(text).ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// Starts one task per chart of a file. Empty charts complete at once.
    /// </summary>
    /// <param name="metadata">The metadata of the file.</param>
    /// <param name="options">The parser options.</param>
    /// <returns>The tasks, in chart order.</returns>
    private static Task<SimaiChart>[] StartChartTasks(SimaiMetadata metadata, SimaiParserOptions? options)
    {
        var tasks = new Task<SimaiChart>[SimaiMetadata.ChartCount];
        var fumens = metadata.Fumens;
        for (var i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = string.IsNullOrEmpty(fumens[i])
                ? Task.FromResult(ParseChartForFile(metadata, index, options))
                : Task.Run(() => ParseChartForFile(metadata, index, options));
        }

        return tasks;
    }

    /// <summary>
    /// Gets whether the charts of a file are worth parsing on several threads: at least two of them have text, and their
    /// text adds up to at least <see cref="ParallelChartTextLength"/> characters. Smaller files parse faster on one
    /// thread, because waking other threads costs more than the work they would take.
    /// </summary>
    /// <param name="fumens">The chart texts of the file; an item may be null.</param>
    /// <returns><see langword="true"/> when the charts should be parsed in parallel.</returns>
    internal static bool ShouldParseChartsInParallel(ReadOnlySpan<string> fumens)
    {
        var chartsWithText = 0;
        var totalLength = 0L;
        foreach (var fumen in fumens)
        {
            if (!string.IsNullOrEmpty(fumen))
            {
                chartsWithText++;
                totalLength += fumen.Length;
            }
        }

        return chartsWithText >= 2 && totalLength >= ParallelChartTextLength;
    }

    /// <summary>
    /// Parses chart <paramref name="index"/> of a file into <paramref name="charts"/>, or stores the exception that
    /// stopped it in <paramref name="errors"/>, so the caller can parse charts in any order and throw the first error.
    /// </summary>
    /// <param name="metadata">The metadata of the file.</param>
    /// <param name="index">The zero based chart index.</param>
    /// <param name="options">The parser options.</param>
    /// <param name="charts">Receives the chart at <paramref name="index"/>.</param>
    /// <param name="errors">Receives the exception at <paramref name="index"/>.</param>
    private static void ParseChartForFile(in SimaiMetadata metadata,
                                          int index,
                                          SimaiParserOptions? options,
                                          SimaiChart[] charts,
                                          Exception?[] errors)
    {
        try
        {
            charts[index] = ParseChartForFile(metadata, index, options);
        }
        catch (Exception e)
        {
            errors[index] = e;
        }
    }

    /// <summary>
    /// Parses chart <paramref name="index"/> of a file. In lenient mode a chart that fails keeps its text but has no
    /// timing points; in strict mode the exception is thrown.
    /// </summary>
    /// <param name="metadata">The metadata of the file.</param>
    /// <param name="index">The zero based chart index.</param>
    /// <param name="options">The parser options.</param>
    /// <returns>The chart.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown in strict mode when the chart breaks the grammar.</exception>
    private static SimaiChart ParseChartForFile(in SimaiMetadata metadata, int index, SimaiParserOptions? options)
    {
        var level = metadata.Levels[index] ?? string.Empty;
        var designer = metadata.Designers[index] ?? string.Empty;
        var fumen = metadata.Fumens[index] ?? string.Empty;
        try
        {
            return ParseChart(level, designer, fumen, options, true);
        }
        catch (Exception e) when (options?.IsStrict != true)
        {
            SimaiLogger.Error($"Failed to parse chart {index + 1} (&inote_{index + 1}) of \"{metadata.Title}\" (hash {metadata.Hash}).", e);

            return SimaiChart.Create(level, designer, fumen, Array.Empty<SimaiTimingPoint>(), Array.Empty<CommaTiming>());
        }
    }

    /// <summary>
    /// Throws the first exception of a list, in list order.
    /// </summary>
    /// <param name="errors">The exceptions, with <see langword="null"/> where there was none.</param>
    private static void ThrowFirst(Exception?[] errors)
    {
        foreach (var error in errors)
        {
            if (error is not null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }
    }

    /// <summary>
    /// Logs a parsed file at the info level.
    /// </summary>
    /// <param name="metadata">The metadata of the file.</param>
    /// <param name="charts">The parsed charts.</param>
    private static void LogParsedFile(in SimaiMetadata metadata, SimaiChart[] charts)
    {
        if (!SimaiLogger.IsEnabled(SimaiLogLevel.Info))
        {
            return;
        }
        var timingCount = 0;
        foreach (var chart in charts)
        {
            timingCount += chart.NoteTimings.Length;
        }
        SimaiLogger.Info($"Parsed \"{metadata.Title}\" (hash {metadata.Hash}): {timingCount} note timing points in 7 charts.");
    }
}
