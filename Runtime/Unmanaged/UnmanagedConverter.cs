#if NET7_0_OR_GREATER
using System;
using System.Runtime.InteropServices;
using System.Text;
using MajSimai.Parsing;

namespace MajSimai.Unmanaged;

/// <summary>
/// Copies parsed results into native memory. A file and everything it points to are copied into one block allocated
/// with <see cref="Marshal.AllocHGlobal(nint)"/>, with the <see cref="UnmanagedSimaiFile"/> at its start, so freeing the
/// file is one <see cref="Marshal.FreeHGlobal(nint)"/> of that pointer. Texts are copied as zero-terminated UTF-8, and
/// their lengths are counted in bytes; empty texts and arrays are null pointers with a length of 0.
/// </summary>
/// <remarks>
/// The copy takes two passes over the file: the first adds up the size of every part, the second allocates the block
/// and writes the parts into it with a <see cref="NativeBlockWriter"/>. Parts that the first pass did not count make
/// the writer throw rather than write past the block, and a copy that throws frees the block before rethrowing.
/// Comma timing points that a parsed chart has not created as objects yet are copied from their values, without
/// creating one object per comma.
/// </remarks>
internal static unsafe class UnmanagedConverter
{
    /// <summary>
    /// Copies a file and everything it contains into one block of native memory.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The native file, at the start of the block; free it with <see cref="Marshal.FreeHGlobal(nint)"/>.</returns>
    /// <exception cref="OutOfMemoryException">Thrown when the block cannot be allocated.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the parts do not fill the block as counted, which is a bug.</exception>
    public static UnmanagedSimaiFile* ToUnmanaged(SimaiFile file)
    {
        var size = SizeOf(file);
        var block = (byte*)Marshal.AllocHGlobal((nint)size);
        try
        {
            var writer = new NativeBlockWriter(block, size);
            var native = writer.Allocate<UnmanagedSimaiFile>(1);
            *native = Write(ref writer, file);
            if (writer.Used != size)
            {
                throw new InvalidOperationException($"The native copy of the file takes {writer.Used} bytes, but {size} were counted.");
            }

            return native;
        }
        catch
        {
            Marshal.FreeHGlobal((nint)block);
            throw;
        }
    }

    /// <summary>
    /// Gets the size of the block that <see cref="ToUnmanaged(SimaiFile)"/> allocates for a file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The size in bytes, padding included.</returns>
    internal static long SizeOf(SimaiFile file)
    {
        var charts = file.Charts;
        var commands = file.Commands;
        var size = NativeBlockWriter.SizeOfArray<UnmanagedSimaiFile>(1)
                   + NativeBlockWriter.SizeOfUtf8(file.Title)
                   + NativeBlockWriter.SizeOfUtf8(file.Artist)
                   + NativeBlockWriter.SizeOfUtf8(file.FinalDesigner)
                   + NativeBlockWriter.SizeOfArray<UnmanagedSimaiChart>(charts.Length)
                   + NativeBlockWriter.SizeOfArray<UnmanagedSimaiCommand>(commands.Count);
        foreach (var chart in charts)
        {
            size += SizeOf(chart ?? SimaiChart.Empty);
        }
        foreach (var command in commands)
        {
            size += NativeBlockWriter.SizeOfUtf8(command.Prefix) + NativeBlockWriter.SizeOfUtf8(command.Value);
        }

        return size;
    }

    /// <summary>
    /// Copies a string to its own native memory as UTF-8 followed by a zero byte, for the error texts of a result.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="length">Receives the length of the UTF-8 text in bytes, without the zero byte.</param>
    /// <returns>
    /// The native text, to free with <see cref="Marshal.FreeHGlobal(nint)"/>, or <see langword="null"/> when
    /// <paramref name="value"/> is null or empty.
    /// </returns>
    /// <exception cref="OutOfMemoryException">Thrown when the memory cannot be allocated.</exception>
    public static byte* ToUtf8(string? value, out int length)
    {
        length = 0;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var buffer = (byte*)Marshal.AllocHGlobal(byteCount + 1);
        Encoding.UTF8.GetBytes(value, new Span<byte>(buffer, byteCount));
        buffer[byteCount] = 0;
        length = byteCount;

        return buffer;
    }

    /// <summary>
    /// Gets the size of a chart and its timing points in the block.
    /// </summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The size in bytes.</returns>
    private static long SizeOf(SimaiChart chart)
    {
        var size = NativeBlockWriter.SizeOfUtf8(chart.Level)
                   + NativeBlockWriter.SizeOfUtf8(chart.Designer)
                   + NativeBlockWriter.SizeOfUtf8(chart.Fumen)
                   + SizeOf(chart.NoteTimings);
        if (chart.TryGetCommaData(out var commaData))
        {
            // Comma values become timing points without notes or text, so only the array takes space.
            return size + NativeBlockWriter.SizeOfArray<UnmanagedSimaiTimingPoint>(commaData.Length);
        }

        return size + SizeOf(chart.CommaTimings);
    }

    /// <summary>
    /// Gets the size of an array of timing points and of their notes in the block.
    /// </summary>
    /// <param name="timings">The timing points.</param>
    /// <returns>The size in bytes.</returns>
    private static long SizeOf(ReadOnlySpan<SimaiTimingPoint> timings)
    {
        var size = NativeBlockWriter.SizeOfArray<UnmanagedSimaiTimingPoint>(timings.Length);
        foreach (var timing in timings)
        {
            var notes = timing.Notes;
            size += NativeBlockWriter.SizeOfUtf8(timing.RawContent) + NativeBlockWriter.SizeOfArray<UnmanagedSimaiNote>(notes.Length);
            foreach (var note in notes)
            {
                size += NativeBlockWriter.SizeOfUtf8(note.RawContent) + NativeBlockWriter.SizeOfArray<int>(note.RelatedNotes.Length);
            }
        }

        return size;
    }

    /// <summary>
    /// Writes a file and everything it contains into the block.
    /// </summary>
    /// <param name="writer">The writer of the block.</param>
    /// <param name="file">The file.</param>
    /// <returns>The native file, whose pointers point into the block.</returns>
    private static UnmanagedSimaiFile Write(ref NativeBlockWriter writer, SimaiFile file)
    {
        var native = new UnmanagedSimaiFile { offset = file.Offset };
        native.title = writer.WriteUtf8(file.Title, out native.titleLen);
        native.artist = writer.WriteUtf8(file.Artist, out native.artistLen);
        native.finalDesigner = writer.WriteUtf8(file.FinalDesigner, out native.finalDesignerLen);
        var charts = file.Charts;
        native.charts = writer.Allocate<UnmanagedSimaiChart>(charts.Length);
        native.chartsLen = charts.Length;
        for (var i = 0; i < charts.Length; i++)
        {
            native.charts[i] = Write(ref writer, charts[i] ?? SimaiChart.Empty);
        }
        var commands = file.Commands;
        native.commands = writer.Allocate<UnmanagedSimaiCommand>(commands.Count);
        native.commandsLen = commands.Count;
        for (var i = 0; i < commands.Count; i++)
        {
            native.commands[i] = Write(ref writer, commands[i]);
        }

        return native;
    }

    /// <summary>
    /// Writes a chart and its timing points into the block.
    /// </summary>
    /// <param name="writer">The writer of the block.</param>
    /// <param name="chart">The chart.</param>
    /// <returns>The native chart, whose pointers point into the block.</returns>
    private static UnmanagedSimaiChart Write(ref NativeBlockWriter writer, SimaiChart chart)
    {
        var native = new UnmanagedSimaiChart { isEmpty = chart.IsEmpty };
        native.level = writer.WriteUtf8(chart.Level, out native.levelLen);
        native.designer = writer.WriteUtf8(chart.Designer, out native.designerLen);
        native.fumen = writer.WriteUtf8(chart.Fumen, out native.fumenLen);
        var noteTimings = chart.NoteTimings;
        native.noteTimings = writer.Allocate<UnmanagedSimaiTimingPoint>(noteTimings.Length);
        native.noteTimingsLen = noteTimings.Length;
        for (var i = 0; i < noteTimings.Length; i++)
        {
            native.noteTimings[i] = Write(ref writer, noteTimings[i]);
        }
        if (chart.TryGetCommaData(out var commaData))
        {
            native.commaTimings = writer.Allocate<UnmanagedSimaiTimingPoint>(commaData.Length);
            native.commaTimingsLen = commaData.Length;
            for (var i = 0; i < commaData.Length; i++)
            {
                native.commaTimings[i] = ToUnmanaged(commaData[i]);
            }
        }
        else
        {
            var commaTimings = chart.CommaTimings;
            native.commaTimings = writer.Allocate<UnmanagedSimaiTimingPoint>(commaTimings.Length);
            native.commaTimingsLen = commaTimings.Length;
            for (var i = 0; i < commaTimings.Length; i++)
            {
                native.commaTimings[i] = Write(ref writer, commaTimings[i]);
            }
        }

        return native;
    }

    /// <summary>
    /// Writes a command into the block.
    /// </summary>
    /// <param name="writer">The writer of the block.</param>
    /// <param name="command">The command.</param>
    /// <returns>The native command, whose pointers point into the block.</returns>
    private static UnmanagedSimaiCommand Write(ref NativeBlockWriter writer, SimaiCommand command)
    {
        var native = new UnmanagedSimaiCommand();
        native.prefix = writer.WriteUtf8(command.Prefix, out native.prefixLen);
        native.value = writer.WriteUtf8(command.Value, out native.valueLen);

        return native;
    }

    /// <summary>
    /// Writes a timing point and its notes into the block. Related notes become indices into the notes of the timing point.
    /// </summary>
    /// <param name="writer">The writer of the block.</param>
    /// <param name="timing">The timing point.</param>
    /// <returns>The native timing point, whose pointers point into the block.</returns>
    private static UnmanagedSimaiTimingPoint Write(ref NativeBlockWriter writer, SimaiTimingPoint timing)
    {
        var native = new UnmanagedSimaiTimingPoint
        {
            timing = timing.Timing,
            bpm = timing.Bpm,
            hSpeed = timing.HSpeed,
            sVeloc = timing.SVeloc,
            rawTextPositionX = timing.RawTextPositionX,
            rawTextPositionY = timing.RawTextPositionY,
            rawTextPosition = timing.RawTextPosition,
            isEmpty = timing.IsEmpty,
        };
        native.rawContent = writer.WriteUtf8(timing.RawContent, out native.rawContentLen);
        var notes = timing.Notes;
        native.notes = writer.Allocate<UnmanagedSimaiNote>(notes.Length);
        native.notesLen = notes.Length;
        for (var i = 0; i < notes.Length; i++)
        {
            native.notes[i] = Write(ref writer, notes[i], notes);
        }

        return native;
    }

    /// <summary>
    /// Writes a note into the block.
    /// </summary>
    /// <param name="writer">The writer of the block.</param>
    /// <param name="note">The note.</param>
    /// <param name="siblings">The notes of the timing point, used to turn related notes into indices.</param>
    /// <returns>
    /// The native note, whose pointers point into the block; a related note that is not in <paramref name="siblings"/>
    /// has the index -1.
    /// </returns>
    private static UnmanagedSimaiNote Write(ref NativeBlockWriter writer, SimaiNote note, SimaiNote[] siblings)
    {
        var native = new UnmanagedSimaiNote
        {
            type = note.Type,
            startPosition = note.StartPosition,
            holdTime = note.HoldTime,
            slideStartTime = note.SlideStartTime,
            slideTime = note.SlideTime,
            touchArea = note.TouchArea,
            isBreak = note.IsBreak,
            isEx = note.IsEx,
            isFakeRotate = note.IsFakeRotate,
            isForceStar = note.IsForceStar,
            isHanabi = note.IsHanabi,
            isTapHeadSlide = note.IsTapHeadSlide,
            isMine = note.IsMine,
            usingSV = note.UsingSV,
            isEach = note.IsEach,
            isStar = note.IsStar,
            isStarDouble = note.IsStarDouble,
        };
        native.rawContent = writer.WriteUtf8(note.RawContent, out native.rawContentLen);
        var related = note.RelatedNotes;
        native.relatedNotes = writer.Allocate<int>(related.Length);
        native.relatedNotesLen = related.Length;
        for (var i = 0; i < related.Length; i++)
        {
            native.relatedNotes[i] = Array.IndexOf(siblings, related[i]);
        }

        return native;
    }

    /// <summary>
    /// Converts the values of a comma timing point, the same values the timing point object would have: no notes and no
    /// text.
    /// </summary>
    /// <param name="comma">The values of the comma.</param>
    /// <returns>The native timing point.</returns>
    private static UnmanagedSimaiTimingPoint ToUnmanaged(in CommaTiming comma)
    {
        return new UnmanagedSimaiTimingPoint
        {
            timing = comma.Timing,
            bpm = comma.Bpm,
            hSpeed = comma.HSpeed,
            sVeloc = comma.SVeloc,
            rawTextPositionX = comma.TextPositionX,
            rawTextPositionY = comma.TextPositionY,
            rawTextPosition = comma.TextPosition,
            isEmpty = true,
        };
    }
}
#endif
