using System;
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
    /// <param name="allowParallel">Whether the notes of a long chart may be read on several threads.</param>
    /// <returns>The parsed chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for other failures.</exception>
    public static SimaiChart Parse(string level, string designer, ReadOnlySpan<char> fumen, string? fumenString, bool allowParallel)
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

        var scanner = new ChartScanner(fumen, contentArena, rawTimings, commaTimings, noteText, declaration);
        scanner.Scan();

        var noteTimings = ReadNotes(rawTimings, contentArena, allowParallel);

        return SimaiChart.Create(level, designer, fumenString ?? fumen.ToString(), noteTimings, commaTimings.ToArray());
    }

    /// <summary>
    /// Reads the notes of every raw timing point.
    /// </summary>
    private static SimaiTimingPoint[] ReadNotes(PooledBuffer<RawNoteTiming> rawTimings, PooledBuffer<char> contentArena, bool allowParallel)
    {
        var count = rawTimings.Count;
        if (count == 0)
        {
            return Array.Empty<SimaiTimingPoint>();
        }
        var result = new SimaiTimingPoint[count];
        if (!allowParallel || count <= ParallelThreshold)
        {
            ReadRange(rawTimings, contentArena, result, 0, count);
            return result;
        }
        var chunkCount = (count + ChunkSize - 1) / ChunkSize;
        Parallel.For(0, chunkCount, chunk =>
        {
            var start = chunk * ChunkSize;
            var end = Math.Min(start + ChunkSize, count);
            ReadRange(rawTimings, contentArena, result, start, end);
        });

        return result;
    }

    /// <summary>
    /// Reads the notes of the raw timing points from <paramref name="start"/> up to <paramref name="end"/>.
    /// </summary>
    private static void ReadRange(PooledBuffer<RawNoteTiming> rawTimings,
                                  PooledBuffer<char> contentArena,
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
                workspace.ParsedNotes.Clear();
                NoteReader.ReadTimingPoint(raw.Timing, raw.Bpm, content, workspace.ParsedNotes);
                var notes = NoteAssembler.Assemble(workspace.ParsedNotes.AsSpan(), workspace);
                workspace.ParsedNotes.Clear();
                result[i] = SimaiTimingPoint.Create(raw.Timing,
                                                    notes,
                                                    StringCache.Get(content),
                                                    raw.TextPositionX,
                                                    raw.TextPositionY,
                                                    raw.Bpm,
                                                    raw.HSpeed,
                                                    1f,
                                                    raw.TextPosition,
                                                    0,
                                                    0);
            }
        }
        finally
        {
            NoteWorkspace.Return(workspace);
        }
    }
}
