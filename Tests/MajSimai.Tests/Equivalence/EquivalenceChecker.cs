using System;
using System.IO;
using System.Threading;
using MajSimai.Tests.Legacy;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Parses the same input with the MajSimai 2.2.3 parser and the current parser, and reports the first semantic
/// difference.
/// </summary>
/// <remarks>
/// Legacy results are normalized with <see cref="LegacyNormalizer"/>, and current results are projected with
/// <see cref="LegacyProjection"/>. When either parser throws, both must throw an exception of the same type with the same
/// message and position.
/// </remarks>
internal sealed class EquivalenceChecker
{
    /// <summary>
    /// The number of files that failed in both parsers.
    /// </summary>
    private long _failedFiles;

    /// <summary>
    /// The number of charts that failed in both parsers.
    /// </summary>
    private long _failedCharts;

    /// <summary>
    /// The number of legacy notes compared.
    /// </summary>
    private long _comparedNotes;

    /// <summary>
    /// The number of charts that threw in both parsers.
    /// </summary>
    private long _chartErrors;

    /// <summary>
    /// Creates a checker that uses the shared legacy parser.
    /// </summary>
    public EquivalenceChecker()
    {
        Legacy = LegacyParser.Instance;
    }

    /// <summary>
    /// Gets the legacy parser.
    /// </summary>
    public LegacyParser Legacy { get; }

    /// <summary>
    /// Gets the normalizer applied to legacy results; its counters report how often it changed something.
    /// </summary>
    public LegacyNormalizer Normalizer { get; } = new();

    /// <summary>
    /// Gets how many files failed in both parsers.
    /// </summary>
    public long FailedFiles => Interlocked.Read(ref _failedFiles);

    /// <summary>
    /// Gets how many charts of parsed files failed in both parsers.
    /// </summary>
    public long FailedCharts => Interlocked.Read(ref _failedCharts);

    /// <summary>
    /// Gets how many legacy notes <see cref="CheckChart"/> compared.
    /// </summary>
    public long ComparedNotes => Interlocked.Read(ref _comparedNotes);

    /// <summary>
    /// Gets how many charts given to <see cref="CheckChart"/> threw in both parsers.
    /// </summary>
    public long ChartErrors => Interlocked.Read(ref _chartErrors);

    /// <summary>
    /// Compares the parse of a whole file, its metadata and its deparsed text.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    public string? CheckFile(byte[] content)
    {
        var metadataDifference = CheckMetadata(content);
        if (metadataDifference is not null)
        {
            return "Metadata: " + metadataDifference;
        }
        LegacyFileView? expected = null;
        object? legacyFile = null;
        Exception? expectedError = null;
        try
        {
            expected = Legacy.ParseFile(content, out legacyFile);
        }
        catch (Exception e)
        {
            expectedError = e;
        }
        SimaiFile? actualFile = null;
        Exception? actualError = null;
        try
        {
            using var stream = new MemoryStream(content, false);
            actualFile = SimaiParser.Parse(stream);
        }
        catch (Exception e)
        {
            actualError = e;
        }
        if (expectedError is not null || actualError is not null)
        {
            var failureDifference = CompareFailures(expectedError, actualError);
            if (failureDifference is null)
            {
                Interlocked.Increment(ref _failedFiles);
            }
            return failureDifference;
        }
        var normalized = expected! with { Charts = Array.ConvertAll(expected!.Charts, Normalizer.Normalize) };
        var difference = ViewComparer.FindDifference(normalized, LegacyProjection.Project(actualFile!));
        if (difference is not null)
        {
            return difference;
        }
        foreach (var chart in actualFile!.Charts)
        {
            if (chart.Fumen.Length != 0 && chart.CommaTimings.IsEmpty)
            {
                Interlocked.Increment(ref _failedCharts);
            }
        }
        var expectedText = Legacy.Deparse(legacyFile!);
        var actualText = SimaiParser.Deparse(actualFile);

        return expectedText == actualText ? null : "Deparse: the texts differ.";
    }

    /// <summary>
    /// Compares the metadata of a file.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    public string? CheckMetadata(byte[] content)
    {
        LegacyMetadataView? expected = null;
        Exception? expectedError = null;
        try
        {
            expected = Legacy.ParseMetadata(content);
        }
        catch (Exception e)
        {
            expectedError = e;
        }
        LegacyMetadataView? actual = null;
        Exception? actualError = null;
        try
        {
            using var stream = new MemoryStream(content, false);
            actual = LegacyProjection.Project(SimaiParser.ParseMetadata(stream));
        }
        catch (Exception e)
        {
            actualError = e;
        }
        if (expectedError is not null || actualError is not null)
        {
            return CompareFailures(expectedError, actualError);
        }

        return ViewComparer.FindDifference(expected!, actual!);
    }

    /// <summary>
    /// Compares the parse of one chart text.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="allowParallel">Whether the current parser may read notes in parallel.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    public string? CheckChart(string fumen, bool allowParallel = true)
    {
        LegacyChartView? expected = null;
        Exception? expectedError = null;
        try
        {
            expected = Legacy.ParseChart("lv", "des", fumen);
        }
        catch (Exception e)
        {
            expectedError = e;
        }
        LegacyChartView? actual = null;
        Exception? actualError = null;
        try
        {
            actual = LegacyProjection.Project(SimaiParser.ParseChart("lv", "des", fumen, allowParallel));
        }
        catch (Exception e)
        {
            actualError = e;
        }
        if (expectedError is not null || actualError is not null)
        {
            var failureDifference = CompareFailures(expectedError, actualError);
            if (failureDifference is null)
            {
                Interlocked.Increment(ref _chartErrors);
            }
            return failureDifference;
        }
        foreach (var timing in expected!.NoteTimings)
        {
            Interlocked.Add(ref _comparedNotes, timing.Notes.Length);
        }

        return ViewComparer.FindDifference(Normalizer.Normalize(expected!), actual!);
    }

    /// <summary>
    /// Compares the exceptions of both parsers.
    /// </summary>
    /// <param name="expected">The legacy exception, if any.</param>
    /// <param name="actual">The current exception, if any.</param>
    /// <returns>A description of the difference, or <see langword="null"/> when both threw the same failure.</returns>
    private static string? CompareFailures(Exception? expected, Exception? actual)
    {
        if (expected is null)
        {
            return $"Only the current parser failed: {actual}";
        }
        if (actual is null)
        {
            return $"Only the legacy parser failed: {expected}";
        }
        var expectedView = ParseFailureView.FromException(expected);
        var actualView = ParseFailureView.FromException(actual);

        return expectedView == actualView ? null : $"Failures differ: expected {expectedView}, actual {actualView}";
    }
}
