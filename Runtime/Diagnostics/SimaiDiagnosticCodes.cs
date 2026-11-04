namespace MajSimai.Diagnostics;

/// <summary>
/// The codes of the problems <see cref="SimaiSyntaxChecker"/> reports. Codes starting with <c>SM1</c> are about the
/// structure of a chart, <c>SM2</c> about notes, <c>SM3</c> about slide paths, and <c>SM4</c> about the fields of a
/// file.
/// </summary>
public static class SimaiDiagnosticCodes
{
    /// <summary>A character that cannot appear here, such as a single <c>|</c>.</summary>
    public const string UnexpectedCharacter = "SM101";

    /// <summary>A <c>(</c>, <c>{</c> or <c>&lt;</c> declaration without its closing character.</summary>
    public const string UnclosedDeclaration = "SM102";

    /// <summary>A BPM, beat or speed declaration whose value cannot be read.</summary>
    public const string InvalidDeclaration = "SM103";

    /// <summary>Note text cut off by a <c>(</c> or <c>{</c> declaration before its comma, such as <c>1(120)2,</c>.</summary>
    public const string NoteCutByDeclaration = "SM104";

    /// <summary>Note text at the end of the chart without a comma after it.</summary>
    public const string UnterminatedNote = "SM105";

    /// <summary>A character outside note text that has no meaning, such as <c>)</c> on its own.</summary>
    public const string StrayCharacter = "SM106";

    /// <summary>A <c>||s</c> time signature that is not two integers such as <c>||s3/4</c>.</summary>
    public const string InvalidSignature = "SM107";

    /// <summary>Text after the end marker <c>E</c>.</summary>
    public const string TextAfterEnd = "SM108";

    /// <summary>An empty part between two <c>`</c> of a fake each.</summary>
    public const string EmptyFakeEachPart = "SM109";

    /// <summary>A note with nothing left after removing its markers, such as <c>b</c>.</summary>
    public const string EmptyNote = "SM201";

    /// <summary>A touch area letter that is not at the start of a note, such as <c>1E</c>.</summary>
    public const string MisplacedTouchArea = "SM202";

    /// <summary>An empty part of a same head group, such as the trailing <c>*</c> of <c>1-5[8:1]*</c>.</summary>
    public const string EmptySameHeadPart = "SM203";

    /// <summary>A duration with reversed brackets, too many separators, or text between the two <c>#</c>.</summary>
    public const string MalformedDuration = "SM204";

    /// <summary>A note that cannot be read and is skipped by the lenient parser.</summary>
    public const string UnreadableNote = "SM205";

    /// <summary>A hold whose duration bracket cannot be read; the lenient parser uses a length of 0.</summary>
    public const string UnreadableHoldLength = "SM206";

    /// <summary>A <c>b</c> or <c>m</c> in the middle of a slide path, which has no effect.</summary>
    public const string IgnoredMarker = "SM207";

    /// <summary>An empty note between two <c>/</c>.</summary>
    public const string EmptyEachPart = "SM208";

    /// <summary>A note whose button, sensor or layout is not valid, such as button 9, <c>B0</c> or <c>1f</c>.</summary>
    public const string InvalidNoteShape = "SM209";

    /// <summary>A slide whose end button is not allowed for its shape, such as <c>1-2</c> or <c>1s4</c>.</summary>
    public const string InvalidSlideEnd = "SM301";

    /// <summary>A slide path that cannot be read, such as an unknown shape or a missing end button.</summary>
    public const string InvalidSlidePath = "SM302";

    /// <summary>A connected slide whose durations are neither on every segment nor only on the last one.</summary>
    public const string InconsistentSlideDuration = "SM303";

    /// <summary>A <c>w</c> slide that is part of a connected slide.</summary>
    public const string WifiInConnectedSlide = "SM304";

    /// <summary>Slide Code that breaks its syntax, such as a digit after <c>C</c> or a missing <c>K</c> end.</summary>
    public const string InvalidSlideCode = "SM310";

    /// <summary>Slide Code that connects a node and a circle that contains it, <c>P</c> directly to <c>Q</c>, or circles 0 and 9.</summary>
    public const string ImpossibleSlideCodeConnection = "SM311";

    /// <summary>Slide Code that goes to the point it is already at.</summary>
    public const string ZeroLengthSlideCode = "SM312";

    /// <summary>Slide Code that goes around the same circle many times, which is hard to read.</summary>
    public const string ManyTurnsSlideCode = "SM313";

    /// <summary>A line starting with <c>&amp;</c> that has no <c>=</c>.</summary>
    public const string FieldWithoutValue = "SM401";

    /// <summary>A <c>&amp;first</c> value that is not a number.</summary>
    public const string InvalidOffset = "SM402";

    /// <summary>A field that is set more than once; only one of the values is used.</summary>
    public const string DuplicateField = "SM403";
}
