#if NET7_0_OR_GREATER
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MajSimai.Unmanaged;

/// <summary>
/// Copies parsed results into native memory. Every pointer is allocated with <see cref="Marshal.AllocHGlobal(int)"/> and
/// is freed by the <c>Free</c> method of the native struct that owns it. Texts are copied as UTF-8, and their lengths
/// are counted in bytes.
/// </summary>
internal static unsafe class UnmanagedConverter
{
    /// <summary>
    /// Copies a file and everything it contains.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The native file.</returns>
    public static UnmanagedSimaiFile ToUnmanaged(SimaiFile file)
    {
        var charts = file.Charts;
        var chartArray = Allocate<UnmanagedSimaiChart>(charts.Length);
        for (var i = 0; i < charts.Length; i++)
        {
            chartArray[i] = ToUnmanaged(charts[i] ?? SimaiChart.Empty);
        }
        var commands = file.Commands;
        var commandArray = Allocate<UnmanagedSimaiCommand>(commands.Count);
        for (var i = 0; i < commands.Count; i++)
        {
            commandArray[i] = ToUnmanaged(commands[i]);
        }
        var native = new UnmanagedSimaiFile
        {
            offset = file.Offset,
            charts = chartArray,
            chartsLen = charts.Length,
            commands = commandArray,
            commandsLen = commands.Count,
        };
        native.title = ToUtf8(file.Title, out native.titleLen);
        native.artist = ToUtf8(file.Artist, out native.artistLen);
        native.finalDesigner = ToUtf8(file.FinalDesigner, out native.finalDesignerLen);

        return native;
    }

    /// <summary>
    /// Copies a chart and its timing points.
    /// </summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The native chart.</returns>
    public static UnmanagedSimaiChart ToUnmanaged(SimaiChart chart)
    {
        var native = new UnmanagedSimaiChart
        {
            isEmpty = chart.IsEmpty,
            noteTimings = ToUnmanaged(chart.NoteTimings),
            noteTimingsLen = chart.NoteTimings.Length,
            commaTimings = ToUnmanaged(chart.CommaTimings),
            commaTimingsLen = chart.CommaTimings.Length,
        };
        native.level = ToUtf8(chart.Level, out native.levelLen);
        native.designer = ToUtf8(chart.Designer, out native.designerLen);
        native.fumen = ToUtf8(chart.Fumen, out native.fumenLen);

        return native;
    }

    /// <summary>
    /// Copies a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The native command.</returns>
    public static UnmanagedSimaiCommand ToUnmanaged(SimaiCommand command)
    {
        var native = new UnmanagedSimaiCommand();
        native.prefix = ToUtf8(command.Prefix, out native.prefixLen);
        native.value = ToUtf8(command.Value, out native.valueLen);

        return native;
    }

    /// <summary>
    /// Copies a timing point and its notes. Related notes become indices into the notes of the timing point.
    /// </summary>
    /// <param name="timing">The timing point.</param>
    /// <returns>The native timing point.</returns>
    public static UnmanagedSimaiTimingPoint ToUnmanaged(SimaiTimingPoint timing)
    {
        var notes = timing.Notes;
        var noteArray = Allocate<UnmanagedSimaiNote>(notes.Length);
        for (var i = 0; i < notes.Length; i++)
        {
            noteArray[i] = ToUnmanaged(notes[i], notes);
        }
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
            notes = noteArray,
            notesLen = notes.Length,
        };
        native.rawContent = ToUtf8(timing.RawContent, out native.rawContentLen);

        return native;
    }

    /// <summary>
    /// Copies a note.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="siblings">The notes of the timing point, used to turn related notes into indices.</param>
    /// <returns>The native note; a related note that is not in <paramref name="siblings"/> has the index -1.</returns>
    public static UnmanagedSimaiNote ToUnmanaged(SimaiNote note, SimaiNote[] siblings)
    {
        var related = note.RelatedNotes;
        var relatedArray = Allocate<int>(related.Length);
        for (var i = 0; i < related.Length; i++)
        {
            relatedArray[i] = Array.IndexOf(siblings, related[i]);
        }
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
            relatedNotes = relatedArray,
            relatedNotesLen = related.Length,
        };
        native.rawContent = ToUtf8(note.RawContent, out native.rawContentLen);

        return native;
    }

    /// <summary>
    /// Copies a string to native memory as UTF-8 followed by a zero byte.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="length">Receives the length of the UTF-8 text in bytes, without the zero byte.</param>
    /// <returns>The native text, or <see langword="null"/> when <paramref name="value"/> is null or empty.</returns>
    public static byte* ToUtf8(string? value, out int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            length = 0;
            return null;
        }
        length = Encoding.UTF8.GetByteCount(value);
        var buffer = (byte*)Marshal.AllocHGlobal(length + 1);
        Encoding.UTF8.GetBytes(value, new Span<byte>(buffer, length));
        buffer[length] = 0;

        return buffer;
    }

    /// <summary>
    /// Copies timing points.
    /// </summary>
    /// <param name="timings">The timing points.</param>
    /// <returns>The native array, or null when there are none.</returns>
    private static UnmanagedSimaiTimingPoint* ToUnmanaged(ReadOnlySpan<SimaiTimingPoint> timings)
    {
        var array = Allocate<UnmanagedSimaiTimingPoint>(timings.Length);
        for (var i = 0; i < timings.Length; i++)
        {
            array[i] = ToUnmanaged(timings[i]);
        }

        return array;
    }

    /// <summary>
    /// Allocates native memory for <paramref name="length"/> items, or returns <see langword="null"/> for none.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="length">The number of items.</param>
    /// <returns>The native memory, or <see langword="null"/>.</returns>
    private static T* Allocate<T>(int length) where T : unmanaged
    {
        if (length == 0)
        {
            return null;
        }

        return (T*)Marshal.AllocHGlobal(sizeof(T) * length);
    }
}
#endif
