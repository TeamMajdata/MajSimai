using System;
using System.Collections.Generic;

namespace MajSimai.Tests.Legacy;

/// <summary>
/// A note in the MajSimai 2.x shape, where a slide note carries its own star head.
/// </summary>
/// <param name="Type">The note type, as the integer value of <c>SimaiNoteType</c>.</param>
/// <param name="StartPosition">The button or sensor index of the note.</param>
/// <param name="HoldTime">The hold length in seconds.</param>
/// <param name="IsBreak">Whether the note (or the star head of a slide) is a break note.</param>
/// <param name="IsEx">Whether the note (or the star head of a slide) is an EX note.</param>
/// <param name="IsFakeRotate">Whether the star uses the fake rotation marker <c>$$</c>.</param>
/// <param name="IsForceStar">Whether the note uses the force star marker <c>$</c>.</param>
/// <param name="IsHanabi">Whether the touch note uses the hanabi marker <c>f</c>.</param>
/// <param name="IsSlideBreak">Whether the slide path is a break slide.</param>
/// <param name="IsSlideNoHead">Whether the slide has no star head of its own.</param>
/// <param name="IsTapHeadSlide">Whether the slide uses the tap head marker <c>@</c>.</param>
/// <param name="IsMine">Whether the note (or the star head of a slide) is a mine.</param>
/// <param name="IsMineSlide">Whether the slide path is a mine slide.</param>
/// <param name="UsingSV">Whether the note follows the slide velocity (no <c>c</c> marker).</param>
/// <param name="RawContent">The note text without markers.</param>
/// <param name="SlideStartTime">The time in seconds at which the slide path starts moving.</param>
/// <param name="SlideTime">The slide path length in seconds.</param>
/// <param name="TouchArea">The touch area letter, or a space for button notes.</param>
internal readonly record struct LegacyNoteView(int Type,
                                               int StartPosition,
                                               double HoldTime,
                                               bool IsBreak,
                                               bool IsEx,
                                               bool IsFakeRotate,
                                               bool IsForceStar,
                                               bool IsHanabi,
                                               bool IsSlideBreak,
                                               bool IsSlideNoHead,
                                               bool IsTapHeadSlide,
                                               bool IsMine,
                                               bool IsMineSlide,
                                               bool UsingSV,
                                               string RawContent,
                                               double SlideStartTime,
                                               double SlideTime,
                                               char TouchArea)
{
    /// <summary>
    /// The integer value of <c>SimaiNoteType.Slide</c>, which did not change between 2.x and 3.x.
    /// </summary>
    public const int SlideType = 1;
}

/// <summary>
/// A timing point in the MajSimai 2.x shape.
/// </summary>
/// <param name="Timing">The time of the timing point in seconds.</param>
/// <param name="Bpm">The BPM in effect.</param>
/// <param name="HSpeed">The note speed multiplier in effect.</param>
/// <param name="SVeloc">The slide velocity multiplier in effect.</param>
/// <param name="RawContent">The note text of the timing point without whitespace.</param>
/// <param name="RawTextPositionX">The column of the timing point in the chart text.</param>
/// <param name="RawTextPositionY">The line of the timing point in the chart text.</param>
/// <param name="RawTextPosition">The character offset of the timing point in the chart text.</param>
/// <param name="SignatureNumerator">The numerator of the time signature in effect.</param>
/// <param name="SignatureDenominator">The denominator of the time signature in effect.</param>
/// <param name="IsEmpty">Whether the timing point has no notes.</param>
/// <param name="Notes">The notes of the timing point.</param>
internal sealed record LegacyTimingView(double Timing,
                                        float Bpm,
                                        float HSpeed,
                                        float SVeloc,
                                        string RawContent,
                                        int RawTextPositionX,
                                        int RawTextPositionY,
                                        int RawTextPosition,
                                        int SignatureNumerator,
                                        int SignatureDenominator,
                                        bool IsEmpty,
                                        LegacyNoteView[] Notes);

/// <summary>
/// A chart in the MajSimai 2.x shape.
/// </summary>
/// <param name="Level">The level text of the chart.</param>
/// <param name="Designer">The designer of the chart.</param>
/// <param name="Fumen">The chart text.</param>
/// <param name="IsEmpty">Whether the chart has no note timing points.</param>
/// <param name="NoteTimings">The timing points that have notes.</param>
/// <param name="CommaTimings">The timing points created for every comma.</param>
internal sealed record LegacyChartView(string Level,
                                       string Designer,
                                       string Fumen,
                                       bool IsEmpty,
                                       LegacyTimingView[] NoteTimings,
                                       LegacyTimingView[] CommaTimings);

/// <summary>
/// A parsed simai file in the MajSimai 2.x shape.
/// </summary>
/// <param name="Title">The song title.</param>
/// <param name="Artist">The song artist.</param>
/// <param name="FinalDesigner">The designer shown for the whole file.</param>
/// <param name="Offset">The <c>&amp;first</c> offset in seconds.</param>
/// <param name="Hash">The hash of the file content.</param>
/// <param name="Charts">The seven charts of the file.</param>
/// <param name="Commands">The custom commands as prefix and value pairs.</param>
internal sealed record LegacyFileView(string Title,
                                      string Artist,
                                      string FinalDesigner,
                                      float Offset,
                                      string Hash,
                                      LegacyChartView[] Charts,
                                      KeyValuePair<string, string>[] Commands);

/// <summary>
/// Parsed simai metadata in the MajSimai 2.x shape.
/// </summary>
/// <param name="Title">The song title.</param>
/// <param name="Artist">The song artist.</param>
/// <param name="FinalDesigner">The designer shown for the whole file.</param>
/// <param name="Offset">The <c>&amp;first</c> offset in seconds.</param>
/// <param name="Hash">The hash of the file content.</param>
/// <param name="Designers">The seven chart designers.</param>
/// <param name="Levels">The seven chart levels.</param>
/// <param name="Fumens">The seven chart texts.</param>
/// <param name="Commands">The custom commands as prefix and value pairs.</param>
internal sealed record LegacyMetadataView(string Title,
                                          string Artist,
                                          string FinalDesigner,
                                          float Offset,
                                          string Hash,
                                          string[] Designers,
                                          string[] Levels,
                                          string[] Fumens,
                                          KeyValuePair<string, string>[] Commands);

/// <summary>
/// An exception thrown by a parser, reduced to the parts a caller can observe.
/// </summary>
/// <param name="ExceptionType">The simple name of the exception type.</param>
/// <param name="Message">The exception message.</param>
/// <param name="Line">The line reported by a markup exception, or <see langword="null"/> for other exceptions.</param>
/// <param name="Column">The column reported by a markup exception, or <see langword="null"/> for other exceptions.</param>
/// <param name="Content">The content reported by a markup exception, or <see langword="null"/> for other exceptions.</param>
internal sealed record ParseFailureView(string ExceptionType, string Message, int? Line, int? Column, string? Content)
{
    /// <summary>
    /// Creates a failure view from an exception thrown by either parser.
    /// </summary>
    /// <param name="exception">The thrown exception; reflection is used so exceptions of the legacy assembly work too.</param>
    /// <returns>The failure view.</returns>
    public static ParseFailureView FromException(Exception exception)
    {
        var type = exception.GetType();
        var line = type.GetProperty("Line")?.GetValue(exception) as int?;
        var column = type.GetProperty("Column")?.GetValue(exception) as int?;
        var content = type.GetProperty("Content")?.GetValue(exception) as string;

        return new ParseFailureView(type.Name, exception.Message, line, column, content);
    }
}
