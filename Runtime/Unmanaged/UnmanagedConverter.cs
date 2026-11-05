#if NET7_0_OR_GREATER
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MajSimai.Unmanaged;

/// <summary>
/// Copies parsed results into native memory. Every pointer is allocated with <see cref="Marshal.AllocHGlobal(int)"/>
/// and is freed by the <c>Free</c> method of the native struct that owns it. Texts are copied as UTF-8, and their
/// lengths are counted in bytes.
/// </summary>
/// <remarks>
/// A copy that fails partway, for example when memory runs out, frees what it already allocated before rethrowing. To
/// make that possible, an array is allocated zeroed and stored in its owner with its length before it is filled: the
/// <c>Free</c> methods skip null pointers, so freeing the owner releases the items copied so far and nothing else.
/// </remarks>
internal static unsafe class UnmanagedConverter
{
    /// <summary>
    /// Copies a file and everything it contains.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The native file.</returns>
    public static UnmanagedSimaiFile ToUnmanaged(SimaiFile file)
    {
        var native = new UnmanagedSimaiFile { offset = file.Offset };
        try
        {
            native.title = ToUtf8(file.Title, out native.titleLen);
            native.artist = ToUtf8(file.Artist, out native.artistLen);
            native.finalDesigner = ToUtf8(file.FinalDesigner, out native.finalDesignerLen);
            var charts = file.Charts;
            native.charts = AllocateZeroed<UnmanagedSimaiChart>(charts.Length);
            native.chartsLen = charts.Length;
            for (var i = 0; i < charts.Length; i++)
            {
                native.charts[i] = ToUnmanaged(charts[i] ?? SimaiChart.Empty);
            }
            var commands = file.Commands;
            native.commands = AllocateZeroed<UnmanagedSimaiCommand>(commands.Count);
            native.commandsLen = commands.Count;
            for (var i = 0; i < commands.Count; i++)
            {
                native.commands[i] = ToUnmanaged(commands[i]);
            }

            return native;
        }
        catch
        {
            native.Free();
            throw;
        }
    }

    /// <summary>
    /// Copies a chart and its timing points.
    /// </summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The native chart.</returns>
    public static UnmanagedSimaiChart ToUnmanaged(SimaiChart chart)
    {
        var native = new UnmanagedSimaiChart();
        try
        {
            native.level = ToUtf8(chart.Level, out native.levelLen);
            native.designer = ToUtf8(chart.Designer, out native.designerLen);
            native.fumen = ToUtf8(chart.Fumen, out native.fumenLen);
            native.noteTimings = AllocateZeroed<UnmanagedSimaiTimingPoint>(chart.NoteTimings.Length);
            native.noteTimingsLen = chart.NoteTimings.Length;
            CopyTimings(chart.NoteTimings, native.noteTimings);
            native.commaTimings = AllocateZeroed<UnmanagedSimaiTimingPoint>(chart.CommaTimings.Length);
            native.commaTimingsLen = chart.CommaTimings.Length;
            CopyTimings(chart.CommaTimings, native.commaTimings);
            native.isEmpty = chart.IsEmpty;

            return native;
        }
        catch
        {
            native.Free();
            throw;
        }
    }

    /// <summary>
    /// Copies a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The native command.</returns>
    public static UnmanagedSimaiCommand ToUnmanaged(SimaiCommand command)
    {
        var native = new UnmanagedSimaiCommand();
        try
        {
            native.prefix = ToUtf8(command.Prefix, out native.prefixLen);
            native.value = ToUtf8(command.Value, out native.valueLen);

            return native;
        }
        catch
        {
            native.Free();
            throw;
        }
    }

    /// <summary>
    /// Copies a timing point and its notes. Related notes become indices into the notes of the timing point.
    /// </summary>
    /// <param name="timing">The timing point.</param>
    /// <returns>The native timing point.</returns>
    public static UnmanagedSimaiTimingPoint ToUnmanaged(SimaiTimingPoint timing)
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
        try
        {
            native.rawContent = ToUtf8(timing.RawContent, out native.rawContentLen);
            var notes = timing.Notes;
            native.notes = AllocateZeroed<UnmanagedSimaiNote>(notes.Length);
            native.notesLen = notes.Length;
            for (var i = 0; i < notes.Length; i++)
            {
                native.notes[i] = ToUnmanaged(notes[i], notes);
            }

            return native;
        }
        catch
        {
            native.Free();
            throw;
        }
    }

    /// <summary>
    /// Copies a note.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="siblings">The notes of the timing point, used to turn related notes into indices.</param>
    /// <returns>The native note; a related note that is not in <paramref name="siblings"/> has the index -1.</returns>
    public static UnmanagedSimaiNote ToUnmanaged(SimaiNote note, SimaiNote[] siblings)
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
        try
        {
            native.rawContent = ToUtf8(note.RawContent, out native.rawContentLen);
            var related = note.RelatedNotes;
            native.relatedNotes = AllocateZeroed<int>(related.Length);
            native.relatedNotesLen = related.Length;
            for (var i = 0; i < related.Length; i++)
            {
                native.relatedNotes[i] = Array.IndexOf(siblings, related[i]);
            }

            return native;
        }
        catch
        {
            native.Free();
            throw;
        }
    }

    /// <summary>
    /// Copies a string to native memory as UTF-8 followed by a zero byte.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="length">Receives the length of the UTF-8 text in bytes, without the zero byte.</param>
    /// <returns>The native text, or <see langword="null"/> when <paramref name="value"/> is null or empty.</returns>
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
    /// Allocates native memory for <paramref name="length"/> items and fills it with zeros, or returns
    /// <see langword="null"/> for none.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="length">The number of items.</param>
    /// <returns>The native memory, or <see langword="null"/>.</returns>
    internal static T* AllocateZeroed<T>(int length) where T : unmanaged
    {
        if (length == 0)
        {
            return null;
        }
        var size = checked(sizeof(T) * length);
        var memory = (T*)Marshal.AllocHGlobal(size);
        new Span<byte>(memory, size).Clear();

        return memory;
    }

    /// <summary>
    /// Copies timing points into a zeroed native array.
    /// </summary>
    /// <param name="timings">The timing points.</param>
    /// <param name="destination">The array, already stored in its owner with its length.</param>
    private static void CopyTimings(ReadOnlySpan<SimaiTimingPoint> timings, UnmanagedSimaiTimingPoint* destination)
    {
        for (var i = 0; i < timings.Length; i++)
        {
            destination[i] = ToUnmanaged(timings[i]);
        }
    }
}
#endif
