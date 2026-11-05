using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MajSimai.Diagnostics;
using MajSimai.Parsing;
using MajSimai.Tests.Legacy;
using MajSimai.Utils;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Parses the same input with MajSimai 2.2.3 and with the current parser in lenient mode, and reports the first
/// difference that is not an intended fix.
/// </summary>
/// <remarks>
/// <para>
/// The legacy parser gets input rewritten by <see cref="LegacyInput"/>, so the end marker <c>E</c>, a final <c>||</c>
/// and trimmed field names mean the same in both. Legacy results are normalized with <see cref="LegacyNormalizer"/>, and
/// current results are projected with <see cref="LegacyProjection"/>.
/// </para>
/// <para>
/// Intended differences that are aligned before comparing: columns (2.x counted them wrongly), the speed of comma timing
/// points (2.x always used 1), and the slide velocity and time signature of note timing points (2.x used 1 and 0/0; they
/// are taken from the 2.x comma timing point at the same comma). Timing points with Slide Code are not compared note by
/// note, because 2.x did not know Slide Code letters.
/// </para>
/// <para>
/// A timing point that holds a note the lenient mode now skips on purpose (no button after the markers, a misplaced touch
/// area letter, an empty <c>*</c> part, a malformed duration) is not compared note by note, because 2.x read those notes
/// differently; it is counted in <see cref="IntendedDifferences"/>. 3.x throws for a declaration that is not closed,
/// where 2.x read to the end of the text; such a failure is intended too. Any other failure in only one parser, or
/// failures with different types or messages, is a difference.
/// </para>
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
    /// The number of timing points with Slide Code whose notes were not compared.
    /// </summary>
    private long _slideCodeTimings;

    /// <summary>
    /// The number of charts cut at the end marker for the legacy parser.
    /// </summary>
    private long _endMarkerCharts;

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
    /// Gets how many charts failed in both parsers.
    /// </summary>
    public long FailedCharts => Interlocked.Read(ref _failedCharts);

    /// <summary>
    /// Gets how many legacy notes were compared.
    /// </summary>
    public long ComparedNotes => Interlocked.Read(ref _comparedNotes);

    /// <summary>
    /// Gets how many timing points with Slide Code were not compared note by note.
    /// </summary>
    public long SlideCodeTimings => Interlocked.Read(ref _slideCodeTimings);

    /// <summary>
    /// Gets how many charts were rewritten for the legacy parser: cut at an end marker <c>E</c>, or with a final
    /// <c>||</c>.
    /// </summary>
    public long EndMarkerCharts => Interlocked.Read(ref _endMarkerCharts);

    /// <summary>
    /// Gets the intended differences, by kind: timing points with notes that 3.x skips, by diagnostic code, and charts
    /// that are not compared because 2.x lost parts of a long fake each.
    /// </summary>
    public ConcurrentDictionary<string, long> IntendedDifferences { get; } = new();

    /// <summary>
    /// Compares the metadata, every chart and the deparsed text of a file.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    public string? CheckFile(byte[] content)
    {
        var text = Decode(content);
        var canonical = Encoding.UTF8.GetBytes(LegacyInput.CanonicalizeFile(text));
        var metadataDifference = CheckMetadata(content, canonical);
        if (metadataDifference is not null)
        {
            return "Metadata: " + metadataDifference;
        }
        SimaiFile file;
        using (var stream = new MemoryStream(content, false))
        {
            file = SimaiParser.Parse(stream);
        }
        for (var i = 0; i < file.Charts.Length; i++)
        {
            var chart = file.Charts[i];
            if (chart.Fumen.Length == 0)
            {
                continue;
            }
            var difference = CheckChart(chart.Fumen, chart.Level, chart.Designer, true);
            if (difference is not null)
            {
                return $"Chart {i + 1}: {difference}";
            }
            difference = CheckFileChart(chart);
            if (difference is not null)
            {
                return $"Chart {i + 1} in the file: {difference}";
            }
        }
        var legacyFile = Legacy.ParseFile(canonical, out var legacyObject);

        return Legacy.Deparse(legacyObject) == SimaiParser.Deparse(file) || legacyFile.Charts.Length != 7
            ? null
            : "Deparse: the texts differ.";
    }

    /// <summary>
    /// Compares the metadata of a file.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <param name="canonical">The bytes rewritten for the legacy parser.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    private string? CheckMetadata(byte[] content, byte[] canonical)
    {
        LegacyMetadataView? expected = null;
        Exception? expectedError = null;
        try
        {
            expected = Legacy.ParseMetadata(canonical);
        }
        catch (Exception e)
        {
            expectedError = e;
        }
        SimaiMetadata metadata = default;
        Exception? actualError = null;
        try
        {
            using var stream = new MemoryStream(content, false);
            metadata = SimaiParser.ParseMetadata(stream);
        }
        catch (Exception e)
        {
            actualError = e;
        }
        if (actualError is not null && IsUnclosedDeclaration(actualError))
        {
            IntendedDifferences.AddOrUpdate("charts with a declaration that is not closed (rejected by 3.x)", 1, (_, count) => count + 1);
            return null;
        }
        if (expectedError is not null || actualError is not null)
        {
            var failure = CompareFailures(expectedError, actualError);
            if (failure is null)
            {
                Interlocked.Increment(ref _failedFiles);
            }
            return failure;
        }
        using (var md5 = MD5.Create())
        {
            var hash = Convert.ToBase64String(md5.ComputeHash(content));
            if (metadata.Hash != hash)
            {
                return $"Hash: expected {hash}, actual {metadata.Hash}";
            }
        }

        return ViewComparer.FindDifference(expected! with { Hash = metadata.Hash }, LegacyProjection.Project(metadata));
    }

    /// <summary>
    /// Compares the parse of one chart text.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="level">The level text.</param>
    /// <param name="designer">The designer.</param>
    /// <param name="allowParallel">Whether the current parser may read notes in parallel.</param>
    /// <returns>The first difference, or <see langword="null"/> when both parsers agree.</returns>
    public string? CheckChart(string fumen, string level = "lv", string designer = "des", bool allowParallel = true)
    {
        var canonical = LegacyInput.CanonicalizeChart(fumen, out var paddedComment);
        if (canonical != fumen)
        {
            Interlocked.Increment(ref _endMarkerCharts);
        }
        if (canonical.Trim().Length == 0)
        {
            return CheckEndOnlyChart(fumen, level, designer);
        }
        LegacyChartView? expected = null;
        Exception? expectedError = null;
        try
        {
            expected = Legacy.ParseChart(level, designer, canonical);
        }
        catch (Exception e)
        {
            expectedError = e;
        }
        SimaiChart? chart = null;
        Exception? actualError = null;
        try
        {
            chart = SimaiParser.ParseChart(level, designer, fumen, null, allowParallel);
        }
        catch (Exception e)
        {
            actualError = e;
        }
        if (actualError is not null && IsUnclosedDeclaration(actualError))
        {
            IntendedDifferences.AddOrUpdate("charts with a declaration that is not closed (rejected by 3.x)", 1, (_, count) => count + 1);
            return null;
        }
        if (expectedError is not null || actualError is not null)
        {
            var failure = CompareFailures(expectedError, actualError);
            if (failure is null)
            {
                Interlocked.Increment(ref _failedCharts);
            }
            return failure;
        }
        if (HasLongFakeEach(fumen))
        {
            // 2.x lost the parts of a fake each after the eighth; 3.x keeps them all.
            IntendedDifferences.AddOrUpdate("charts with a fake each of 9 or more parts (not compared)", 1, (_, count) => count + 1);
            return chart!.NoteTimings.Length >= expected!.NoteTimings.Length ? null : "A long fake each lost parts.";
        }
        foreach (var timing in expected!.NoteTimings)
        {
            Interlocked.Add(ref _comparedNotes, timing.Notes.Length);
        }
        var normalized = Normalizer.Normalize(expected) with { Fumen = fumen };
        var actual = LegacyProjection.Project(chart!);
        Align(ref normalized, ref actual, paddedComment);

        return ViewComparer.FindDifference(normalized, actual);
    }

    /// <summary>
    /// Checks a chart whose text is only an end marker <c>E</c> and what follows it: 3.x reads no notes and one comma
    /// timing point at the marker.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="level">The level text.</param>
    /// <param name="designer">The designer.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? CheckEndOnlyChart(string fumen, string level, string designer)
    {
        var chart = SimaiParser.ParseChart(level, designer, fumen, null, false);
        var isValid = chart.NoteTimings.IsEmpty &&
                      chart.CommaTimings.Length == 1 &&
                      fumen[chart.CommaTimings[0].RawTextPosition] == 'E';

        return isValid ? null : "A chart that starts with the end marker has notes.";
    }

    /// <summary>
    /// Gets whether a chart has a fake each with 9 or more parts, which 2.x cut short.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns><see langword="true"/> when a timing point has 8 or more <c>`</c>.</returns>
    private static bool HasLongFakeEach(string fumen)
    {
        return fumen.Split(',').Any(part => part.Count(c => c == '`') >= 8);
    }

    /// <summary>
    /// Checks that a chart of a parsed file equals the chart parsed alone, or the empty chart of a failed parse.
    /// </summary>
    /// <param name="chart">The chart of the file.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? CheckFileChart(SimaiChart chart)
    {
        SimaiChart expected;
        try
        {
            expected = SimaiParser.ParseChart(chart.Level, chart.Designer, chart.Fumen, null, false);
        }
        catch (Exception)
        {
            return chart.NoteTimings.IsEmpty && chart.CommaTimings.IsEmpty ? null : "The failed chart has timing points.";
        }

        return ModelComparer.FindDifference(expected, chart);
    }

    /// <summary>
    /// Aligns the parts of both results that 3.x changed on purpose, so the rest can be compared.
    /// </summary>
    /// <param name="expected">The normalized legacy chart.</param>
    /// <param name="actual">The projected current chart.</param>
    /// <param name="paddedComment">Whether a space was added after a final <c>||</c> for the legacy parser.</param>
    private void Align(ref LegacyChartView expected, ref LegacyChartView actual, bool paddedComment)
    {
        var commas = expected.CommaTimings.ToDictionary(t => t.RawTextPosition);
        var expectedNotes = expected.NoteTimings.Select(t =>
        {
            var comma = commas.TryGetValue(t.RawTextPosition, out var value) ? value : t;
            return t with
            {
                RawTextPositionX = 0,
                SVeloc = comma.SVeloc,
                SignatureNumerator = comma.SignatureNumerator,
                SignatureDenominator = comma.SignatureDenominator,
            };
        }).ToArray();
        var expectedCommas = expected.CommaTimings.Select(t => t with { RawTextPositionX = 0, HSpeed = 1 }).ToArray();
        if (paddedComment && expectedCommas.Length != 0)
        {
            var last = expectedCommas[^1];
            expectedCommas[^1] = last with { RawTextPosition = last.RawTextPosition - 1 };
        }
        var actualNotes = actual.NoteTimings.Select(t => t with { RawTextPositionX = 0 }).ToArray();
        var actualCommas = actual.CommaTimings.Select(t => t with { RawTextPositionX = 0, HSpeed = 1 }).ToArray();
        for (var i = 0; i < Math.Min(expectedNotes.Length, actualNotes.Length); i++)
        {
            var skip = false;
            if (HasSlideCode(actualNotes[i].RawContent))
            {
                Interlocked.Increment(ref _slideCodeTimings);
                skip = true;
            }
            else if (ViewComparer.FindDifference(expectedNotes[i], actualNotes[i]) is not null)
            {
                var code = FindSkippedNoteCode(actualNotes[i].RawContent);
                if (code is not null)
                {
                    IntendedDifferences.AddOrUpdate($"timing points with a note skipped by 3.x ({code})", 1, (_, count) => count + 1);
                    skip = true;
                }
            }
            if (skip)
            {
                expectedNotes[i] = expectedNotes[i] with { Notes = Array.Empty<LegacyNoteView>(), IsEmpty = true };
                actualNotes[i] = actualNotes[i] with { Notes = Array.Empty<LegacyNoteView>(), IsEmpty = true };
            }
        }
        expected = expected with { NoteTimings = expectedNotes, CommaTimings = expectedCommas };
        actual = actual with { NoteTimings = actualNotes, CommaTimings = actualCommas };
    }

    /// <summary>
    /// Gets whether a timing point has a note that starts with a button and has a Slide Code letter.
    /// </summary>
    /// <param name="rawContent">The note text of the timing point.</param>
    /// <returns><see langword="true"/> when 3.x reads a Slide Code letter that 2.x did not know.</returns>
    private static bool HasSlideCode(string rawContent)
    {
        foreach (var note in rawContent.Split('/', '*'))
        {
            if (note.Length > 1 && char.IsDigit(note[0]) && note.Skip(1).Any(c => c is 'A' or 'B' or 'C' or 'P' or 'Q'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether an exception of the current parser is for a declaration that is not closed.
    /// </summary>
    /// <param name="error">The exception.</param>
    /// <returns><see langword="true"/> for the unclosed declaration error.</returns>
    private static bool IsUnclosedDeclaration(Exception error)
    {
        return error is InvalidSimaiSyntaxException && error.Message.Contains("is not closed", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the notes of a timing point as the lenient mode does, collecting what it skips, and finds a note that the
    /// lenient mode skips on purpose, where 2.x read something else.
    /// </summary>
    /// <param name="rawContent">The note text of the timing point.</param>
    /// <returns>The diagnostic code of the first such note, or <see langword="null"/>.</returns>
    private static string? FindSkippedNoteCode(string rawContent)
    {
        var diagnostics = new List<SimaiDiagnostic>();
        var reporter = new SyntaxReporter(diagnostics, null, 0, SimaiSyntaxTolerance.Lenient);
        using var notes = new PooledBuffer<ParsedNote>();
        NoteReader.ReadTimingPoint(new NoteSource(0, 120, rawContent.AsSpan(), 1, 1, reporter), notes);
        var skippedCodes = new[]
        {
            SimaiDiagnosticCodes.EmptyNote,
            SimaiDiagnosticCodes.MisplacedTouchArea,
            SimaiDiagnosticCodes.EmptySameHeadPart,
            SimaiDiagnosticCodes.MalformedDuration,
        };

        return diagnostics.FirstOrDefault(d => skippedCodes.Contains(d.Code))?.Code;
    }

    /// <summary>
    /// Compares the exceptions of both parsers by type and message.
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
        var expectedView = ParseFailureView.FromException(expected) with { Line = null, Column = null, Content = null };
        var actualView = ParseFailureView.FromException(actual) with { Line = null, Column = null, Content = null };
        if (expectedView == actualView)
        {
            return null;
        }
        // 2.x reported "Unexpected character" for a declaration too short to close; 3.x reads it and reports its value.
        // The legacy exception type lives in another load context, so it is matched by name.
        var isShortDeclaration = expected.GetType().Name == "InvalidSimaiMarkupException" &&
                                 (expected.Message is "Unexpected character \"(\"" or "Unexpected character \"{\"" or "Unexpected character \"<\"") &&
                                 actual is InvalidSimaiMarkupException;

        return isShortDeclaration ? null : $"Failures differ: expected {expectedView}, actual {actualView}";
    }

    /// <summary>
    /// Decodes file bytes the way the parser does: UTF-8, or the encoding of the byte order mark.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The text.</returns>
    private static string Decode(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content, false), Encoding.UTF8, true);

        return reader.ReadToEnd();
    }
}
