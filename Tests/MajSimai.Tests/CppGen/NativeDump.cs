using System;
using System.Globalization;
using System.Text;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Writes the dump that <c>Native/BindingTest.cpp</c> prints, from the C# parser, so the two can be compared exactly.
/// </summary>
internal static class NativeDump
{
    /// <summary>
    /// Writes the dump of a parsed file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The dump, one record per line.</returns>
    public static string Write(SimaiFile file)
    {
        var builder = new StringBuilder();
        builder.Append($"file title={Escape(file.Title)} artist={Escape(file.Artist)} designer={Escape(file.FinalDesigner)} offset={Bits(file.Offset)} charts={file.Charts.Length} commands={file.Commands.Count}\n");
        foreach (var command in file.Commands)
        {
            builder.Append($"command {Escape(command.Prefix)}={Escape(command.Value)}\n");
        }
        foreach (var chart in file.Charts)
        {
            builder.Append($"chart level={Escape(chart.Level)} designer={Escape(chart.Designer)} empty={Flag(chart.IsEmpty)} fumen={Escape(chart.Fumen)}\n");
            foreach (var timing in chart.NoteTimings)
            {
                WriteTiming(builder, "note", timing);
            }
            foreach (var timing in chart.CommaTimings)
            {
                WriteTiming(builder, "comma", timing);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes the record of a parse error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The record.</returns>
    public static string WriteError(int code, string message)
    {
        return $"error code={code} line=-1 column=-1 message={Escape(message)} content=\n";
    }

    /// <summary>
    /// Writes a timing point and its notes.
    /// </summary>
    /// <param name="builder">Receives the records.</param>
    /// <param name="kind">Whether the timing point is a note or a comma timing point.</param>
    /// <param name="timing">The timing point.</param>
    private static void WriteTiming(StringBuilder builder, string kind, SimaiTimingPoint timing)
    {
        builder.Append($"timing kind={kind} t={Bits(timing.Timing)} bpm={Bits(timing.Bpm)} hs={Bits(timing.HSpeed)} sv={Bits(timing.SVeloc)} x={timing.RawTextPositionX} y={timing.RawTextPositionY} pos={timing.RawTextPosition} empty={Flag(timing.IsEmpty)} raw={Escape(timing.RawContent)}\n");
        foreach (var note in timing.Notes)
        {
            var flags = string.Concat(Flag(note.IsBreak), Flag(note.IsEx), Flag(note.IsFakeRotate), Flag(note.IsForceStar), Flag(note.IsHanabi),
                                      Flag(note.IsTapHeadSlide), Flag(note.IsMine), Flag(note.UsingSV), Flag(note.IsEach), Flag(note.IsStar),
                                      Flag(note.IsStarDouble));
            var related = string.Join(",", Array.ConvertAll(note.RelatedNotes, r => Array.IndexOf(timing.Notes, r)));
            builder.Append($"note type={(int)note.Type} pos={note.StartPosition} hold={Bits(note.HoldTime)} slideStart={Bits(note.SlideStartTime)} slide={Bits(note.SlideTime)} area={(int)note.TouchArea} flags={flags} raw={Escape(note.RawContent)} related={related}\n");
        }
    }

    /// <summary>
    /// Escapes backslashes and line breaks like the C++ program.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    private static string Escape(string? text)
    {
        return (text ?? string.Empty).Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>
    /// Writes the bits of a double as hex.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>16 hex digits.</returns>
    private static string Bits(double value)
    {
        return BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes the bits of a float as hex.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>8 hex digits.</returns>
    private static string Bits(float value)
    {
        return BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a flag as 0 or 1.
    /// </summary>
    /// <param name="value">The flag.</param>
    /// <returns><c>1</c> or <c>0</c>.</returns>
    private static string Flag(bool value)
    {
        return value ? "1" : "0";
    }
}
