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
    /// The number of note timing points above which the notes are read in parallel. A typical chart has a few hundred
    /// to a thousand timing points and reads them in well under a millisecond, which waking other threads would not
    /// shorten; only much longer charts gain from it.
    /// </summary>
    internal const int ParallelThreshold = 4096;

    /// <summary>
    /// The number of note timing points one parallel work item reads.
    /// </summary>
    internal const int ChunkSize = 1024;

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
        // Every comma and the end of the text make a comma timing point, and a note timing point ends at a comma (more
        // only with fake each), so sizing both buffers from the commas keeps them from growing and copying what they
        // hold; comma-heavy charts have hundreds of thousands of them.
        var timingCapacity = CountCommas(fumen) + 1;
        using var contentArena = new PooledBuffer<char>(fumen.Length);
        using var rawTimings = new PooledBuffer<RawNoteTiming>(timingCapacity);
        using var commaTimings = new PooledBuffer<CommaTiming>(timingCapacity);
        using var noteText = new PooledBuffer<char>(64);
        using var declaration = new PooledBuffer<char>(16);

        var scanner = new ChartScanner(fumen, reporter, contentArena, rawTimings, commaTimings, noteText, declaration);
        scanner.Scan();

        var noteTimings = ReadNotes(rawTimings, contentArena, reporter, allowParallel && !reporter.IsCollecting);

        return SimaiChart.Create(level, designer, fumenString ?? fumen.ToString(), noteTimings, commaTimings.ToArray());
    }

    /// <summary>
    /// Counts the commas of a chart text, an upper bound of its comma timing points: commas in comments and after the
    /// end marker make none.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The number of commas.</returns>
    private static int CountCommas(ReadOnlySpan<char> fumen)
    {
#if NET8_0_OR_GREATER
        return fumen.Count(',');
#else
        var count = 0;
        foreach (var c in fumen)
        {
            if (c == ',')
            {
                count++;
            }
        }

        return count;
#endif
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
                var parsedNotes = workspace.ParsedNotes.AsSpan();
                var notes = NoteAssembler.Assemble(parsedNotes, workspace);
                // The text of a timing point with one note is often the text of that note; reuse its string.
                var rawContent = parsedNotes.Length == 1 && content.SequenceEqual(parsedNotes[0].RawContent.AsSpan())
                    ? parsedNotes[0].RawContent
                    : StringCache.Get(content);
                workspace.ParsedNotes.Clear();
                result[i] = SimaiTimingPoint.Create(raw.Timing,
                                                    notes,
                                                    rawContent,
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
