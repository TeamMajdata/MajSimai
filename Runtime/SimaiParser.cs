using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MajSimai.Logging;
using MajSimai.Parsing;

namespace MajSimai;

/// <summary>
/// Parses simai files and charts, and writes simai files back to text.
/// </summary>
/// <remarks>
/// The seven charts of a file are parsed in parallel, and the notes of a long chart are read in parallel as well. A
/// chart that fails to parse does not fail the file: it is kept with its text but without timing points, and the error is
/// logged through <see cref="SimaiLogger"/>.
/// </remarks>
public static class SimaiParser
{
    #region Parse

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/>.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(ReadOnlySpan<char> content, string hash)
    {
        return Parse(ParseMetadata(content, hash));
    }

    /// <summary>
    /// Parses the charts of <paramref name="metadata"/> into a <see cref="SimaiFile"/>.
    /// </summary>
    /// <param name="metadata">The metadata read by <see cref="ParseMetadata(ReadOnlySpan{char}, string)"/>.</param>
    /// <returns>The parsed file.</returns>
    public static SimaiFile Parse(SimaiMetadata metadata)
    {
        MakeSureCulture();
        var charts = new SimaiChart[SimaiMetadata.ChartCount];
        Parallel.For(0, charts.Length, i => charts[i] = ParseChartOrFallback(metadata, i));
        LogParsedFile(metadata, charts);

        return new SimaiFile(metadata, charts);
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 (or the encoding of its byte order mark) and parses it into a
    /// <see cref="SimaiFile"/>. The hash is the Base64 MD5 hash of the bytes.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(Stream contentStream)
    {
        return Parse(contentStream, Encoding.UTF8);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> (or the encoding of its byte order mark) and parses it
    /// into a <see cref="SimaiFile"/>. The hash is the Base64 MD5 hash of the bytes.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a field line is malformed.</exception>
    public static SimaiFile Parse(Stream contentStream, Encoding encoding)
    {
        using var content = ContentDecoder.Decode(contentStream, encoding);

        return Parse(content.Span, content.Hash);
    }

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>A task that returns the parsed file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    public static Task<SimaiFile> ParseAsync(string content, string hash)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return ParseAsync(content.AsMemory(), hash);
    }

    /// <summary>
    /// Parses simai text into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text; it must not change until the task completes.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(ReadOnlyMemory<char> content, string hash)
    {
        var metadata = await ParseMetadataAsync(content, hash).ConfigureAwait(false);

        return await ParseAsync(metadata).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the charts of <paramref name="metadata"/> into a <see cref="SimaiFile"/> on the thread pool.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(SimaiMetadata metadata)
    {
        MakeSureCulture();
        var charts = await Task.WhenAll(StartChartTasks(metadata)).ConfigureAwait(false);
        LogParsedFile(metadata, charts);

        return new SimaiFile(metadata, charts);
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and parses it into a <see cref="SimaiFile"/> without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static Task<SimaiFile> ParseAsync(Stream contentStream)
    {
        return ParseAsync(contentStream, Encoding.UTF8);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and parses it into a <see cref="SimaiFile"/> without
    /// blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <returns>A task that returns the parsed file.</returns>
    public static async Task<SimaiFile> ParseAsync(Stream contentStream, Encoding encoding)
    {
        using var content = await ContentDecoder.DecodeAsync(contentStream, encoding).ConfigureAwait(false);

        return await ParseAsync(content.Memory, content.Hash).ConfigureAwait(false);
    }

    #endregion

    #region ParseMetadata

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/>. The notes are not parsed.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(ReadOnlySpan<char> content, string hash)
    {
        MakeSureCulture();

        return MetadataReader.Read(content, hash);
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and reads its fields into <see cref="SimaiMetadata"/>.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(Stream contentStream)
    {
        return ParseMetadata(contentStream, Encoding.UTF8);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and reads its fields into <see cref="SimaiMetadata"/>.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    public static SimaiMetadata ParseMetadata(Stream contentStream, Encoding encoding)
    {
        using var content = ContentDecoder.Decode(contentStream, encoding);

        return ParseMetadata(content.Span, content.Hash);
    }

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(string content, string hash)
    {
        return Task.Run(() => ParseMetadata(content.AsSpan(), hash));
    }

    /// <summary>
    /// Reads the fields of simai text into <see cref="SimaiMetadata"/> on the thread pool.
    /// </summary>
    /// <param name="content">The simai text; it must not change until the task completes.</param>
    /// <param name="hash">The hash to store in the result.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(ReadOnlyMemory<char> content, string hash)
    {
        return Task.Run(() => ParseMetadata(content.Span, hash));
    }

    /// <summary>
    /// Reads a stream to its end with UTF-8 and reads its fields into <see cref="SimaiMetadata"/> without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream)
    {
        return ParseMetadataAsync(contentStream, Encoding.UTF8);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> and reads its fields into <see cref="SimaiMetadata"/>
    /// without blocking.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text.</param>
    /// <returns>A task that returns the metadata.</returns>
    public static async Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream, Encoding encoding)
    {
        using var content = await ContentDecoder.DecodeAsync(contentStream, encoding).ConfigureAwait(false);

        return await ParseMetadataAsync(content.Memory, content.Hash).ConfigureAwait(false);
    }

    #endregion

    #region ParseChart

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> without a level or designer.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for other failures.</exception>
    public static SimaiChart ParseChart(ReadOnlySpan<char> fumen)
    {
        return ParseChart(string.Empty, string.Empty, fumen);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for other failures.</exception>
    public static SimaiChart ParseChart(string level, string designer, ReadOnlySpan<char> fumen)
    {
        MakeSureCulture();

        return ChartParser.Parse(level, designer, fumen, null, true);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/>, keeping the given string as <see cref="SimaiChart.Fumen"/>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="allowParallel">Whether the notes of a long chart may be read on several threads.</param>
    /// <returns>The parsed chart.</returns>
    internal static SimaiChart ParseChart(string level, string designer, string fumen, bool allowParallel)
    {
        MakeSureCulture();

        return ChartParser.Parse(level, designer, fumen.AsSpan(), fumen, allowParallel);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string fumen)
    {
        return ParseChartAsync(string.Empty, string.Empty, fumen);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="fumen">The chart text; it must not change until the task completes.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(ReadOnlyMemory<char> fumen)
    {
        return ParseChartAsync(string.Empty, string.Empty, fumen);
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text; it must not change until the task completes.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string level, string designer, ReadOnlyMemory<char> fumen)
    {
        return Task.Run(() => ParseChart(level, designer, fumen.Span));
    }

    /// <summary>
    /// Parses a chart text into a <see cref="SimaiChart"/> on the thread pool.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <returns>A task that returns the parsed chart.</returns>
    public static Task<SimaiChart> ParseChartAsync(string level, string designer, string fumen)
    {
        return Task.Run(() => ParseChart(level, designer, fumen ?? string.Empty, true));
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
        MakeSureCulture();

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
    private static Task<SimaiChart>[] StartChartTasks(SimaiMetadata metadata)
    {
        var tasks = new Task<SimaiChart>[SimaiMetadata.ChartCount];
        var fumens = metadata.Fumens;
        for (var i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = string.IsNullOrEmpty(fumens[i])
                ? Task.FromResult(ParseChartOrFallback(metadata, index))
                : Task.Run(() => ParseChartOrFallback(metadata, index));
        }

        return tasks;
    }

    /// <summary>
    /// Parses chart <paramref name="index"/> of a file. A chart that fails keeps its text but has no timing points.
    /// </summary>
    private static SimaiChart ParseChartOrFallback(in SimaiMetadata metadata, int index)
    {
        var level = metadata.Levels[index] ?? string.Empty;
        var designer = metadata.Designers[index] ?? string.Empty;
        var fumen = metadata.Fumens[index] ?? string.Empty;
        try
        {
            return ParseChart(level, designer, fumen, true);
        }
        catch (Exception e)
        {
            SimaiLogger.Error($"Failed to parse chart {index + 1} (&inote_{index + 1}) of \"{metadata.Title}\" (hash {metadata.Hash}).", e);

            return SimaiChart.Create(level, designer, fumen, Array.Empty<SimaiTimingPoint>(), Array.Empty<CommaTiming>());
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

    /// <summary>
    /// Makes new threads use the invariant culture, as MajSimai 2.x did. Parsing itself always uses the invariant
    /// culture.
    /// </summary>
    private static void MakeSureCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
}
