using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Parses one chart text into a <see cref="SimaiChart"/>: <see cref="ChartScanner"/> finds the timing points, then the
/// notes of every timing point are read, in parallel for long charts.
/// </summary>
internal static class ChartParser
{
    /// <summary>
    /// The number of note timing points above which the notes are read in parallel.
    /// </summary>
    internal const int ParallelThreshold = 256;

    /// <summary>
    /// The number of note timing points one parallel work item reads.
    /// </summary>
    internal const int ChunkSize = 128;

    /// <summary>
    /// Parses a chart.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="fumenString">The chart text as a string when the caller has one, so it is not copied again.</param>
    /// <param name="reporter">Decides what happens when the text breaks the grammar.</param>
    /// <param name="allowParallel">Whether the notes of a long chart may be read on several threads; ignored for a
    /// collecting reporter.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when the text breaks the grammar in a way the mode does not accept.
    /// When several timing points fail, the first one in the text is reported.</exception>
    public static SimaiChart Parse(string level,
                                   string designer,
                                   ReadOnlySpan<char> fumen,
                                   string? fumenString,
                                   SyntaxReporter reporter,
                                   bool allowParallel)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }
        if (designer is null)
        {
            throw new ArgumentNullException(nameof(designer));
        }
        if (fumen.IsEmpty)
        {
            return SimaiChart.Create(level, designer, string.Empty, Array.Empty<SimaiTimingPoint>(), Array.Empty<CommaTiming>());
        }
        using var contentArena = new PooledBuffer<char>(fumen.Length);
        using var rawTimings = new PooledBuffer<RawNoteTiming>(256);
        using var commaTimings = new PooledBuffer<CommaTiming>(256);
        using var noteText = new PooledBuffer<char>(64);
        using var declaration = new PooledBuffer<char>(16);

        var scanner = new ChartScanner(fumen, reporter, contentArena, rawTimings, commaTimings, noteText, declaration);
        scanner.Scan();

        var noteTimings = ReadNotes(rawTimings, contentArena, reporter, allowParallel && !reporter.IsCollecting);

        return SimaiChart.Create(level, designer, fumenString ?? fumen.ToString(), noteTimings, commaTimings.ToArray());
    }

    /// <summary>
    /// Reads the notes of every raw timing point.
    /// </summary>
    /// <param name="rawTimings">The raw timing points.</param>
    /// <param name="contentArena">The note text of every timing point.</param>
    /// <param name="reporter">Decides what happens when a note breaks the grammar.</param>
    /// <param name="allowParallel">Whether the notes may be read on several threads.</param>
    /// <returns>The note timing points.</returns>
    /// <exception cref="InvalidSimaiMarkupException">Thrown for the first timing point in the text whose notes fail.</exception>
    private static SimaiTimingPoint[] ReadNotes(PooledBuffer<RawNoteTiming> rawTimings,
                                               PooledBuffer<char> contentArena,
                                               SyntaxReporter reporter,
                                               bool allowParallel)
    {
        var count = rawTimings.Count;
        if (count == 0)
        {
            return Array.Empty<SimaiTimingPoint>();
        }
        var result = new SimaiTimingPoint[count];
        if (!allowParallel || count <= ParallelThreshold)
        {
            ReadRange(rawTimings, contentArena, reporter, result, 0, count);
            return result;
        }
        var chunkCount = (count + ChunkSize - 1) / ChunkSize;
        var errors = new Exception?[chunkCount];
        Parallel.For(0, chunkCount, chunk =>
        {
            var start = chunk * ChunkSize;
            var end = Math.Min(start + ChunkSize, count);
            try
            {
                ReadRange(rawTimings, contentArena, reporter, result, start, end);
            }
            catch (Exception e)
            {
                errors[chunk] = e;
            }
        });
        foreach (var error in errors)
        {
            if (error is not null)
            {
                // The chunks are in text order, and each stops at its first failure.
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the notes of the raw timing points from <paramref name="start"/> up to <paramref name="end"/>.
    /// </summary>
    /// <param name="rawTimings">The raw timing points.</param>
    /// <param name="contentArena">The note text of every timing point.</param>
    /// <param name="reporter">Decides what happens when a note breaks the grammar.</param>
    /// <param name="result">Receives the timing points.</param>
    /// <param name="start">The first index to read.</param>
    /// <param name="end">The index after the last one to read.</param>
    private static void ReadRange(PooledBuffer<RawNoteTiming> rawTimings,
                                  PooledBuffer<char> contentArena,
                                  SyntaxReporter reporter,
                                  SimaiTimingPoint[] result,
                                  int start,
                                  int end)
    {
        var workspace = NoteWorkspace.Rent();
        try
        {
            for (var i = start; i < end; i++)
            {
                var raw = rawTimings[i];
                ReadOnlySpan<char> content = contentArena.AsSpan(raw.ContentStart, raw.ContentLength);
                var source = new NoteSource(raw.Timing, raw.Bpm, content, raw.NoteLine, raw.NoteColumn, reporter);
                workspace.ParsedNotes.Clear();
                NoteReader.ReadTimingPoint(source, workspace.ParsedNotes);
                var notes = NoteAssembler.Assemble(workspace.ParsedNotes.AsSpan(), workspace);
                workspace.ParsedNotes.Clear();
                result[i] = SimaiTimingPoint.Create(raw.Timing,
                                                    notes,
                                                    StringCache.Get(content),
                                                    raw.TextPositionX,
                                                    raw.TextPositionY,
                                                    raw.Bpm,
                                                    raw.HSpeed,
                                                    raw.SVeloc,
                                                    raw.TextPosition,
                                                    raw.SignatureNumerator,
                                                    raw.SignatureDenominator);
            }
        }
        finally
        {
            NoteWorkspace.Return(workspace);
        }
    }
}
