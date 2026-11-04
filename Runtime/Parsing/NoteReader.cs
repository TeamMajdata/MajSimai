using System;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// The text of one timing point and where it is in the chart, used to read its notes and to report problems.
/// </summary>
internal readonly ref struct NoteSource
{
    /// <summary>
    /// Creates a note source.
    /// </summary>
    /// <param name="timing">The time of the timing point in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="content">The note text of the timing point, without whitespace.</param>
    /// <param name="line">The line where the note text starts.</param>
    /// <param name="column">The column where the note text starts.</param>
    /// <param name="reporter">Decides what happens when a note breaks the grammar.</param>
    public NoteSource(double timing, double bpm, ReadOnlySpan<char> content, int line, int column, SyntaxReporter reporter)
    {
        Timing = timing;
        Bpm = bpm;
        Content = content;
        Line = line;
        Column = column;
        Reporter = reporter;
    }

    /// <summary>Gets the time of the timing point in seconds.</summary>
    public double Timing { get; }

    /// <summary>Gets the BPM in effect.</summary>
    public double Bpm { get; }

    /// <summary>Gets the note text of the timing point, without whitespace.</summary>
    public ReadOnlySpan<char> Content { get; }

    /// <summary>Gets the line where the note text starts.</summary>
    public int Line { get; }

    /// <summary>Gets the column where the note text starts.</summary>
    public int Column { get; }

    /// <summary>Gets the reporter that decides what happens when a note breaks the grammar.</summary>
    public SyntaxReporter Reporter { get; }

    /// <summary>
    /// Reports a broken rule for a note of this timing point. Returns only when reading can continue.
    /// </summary>
    /// <param name="rule">The broken rule.</param>
    /// <param name="message">The description of the problem.</param>
    /// <param name="note">The note text.</param>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when the rule stops parsing in this mode.</exception>
    public void Report(SyntaxRule rule, string message, ReadOnlySpan<char> note)
    {
        Reporter.Report(rule, Line, Column, note.ToString(), $"{message} (timing point \"{Content.ToString()}\")");
    }

    /// <summary>
    /// Gets whether reporting a broken rule has any effect; see <see cref="SyntaxReporter.IsReported"/>.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when the report would collect, throw or log.</returns>
    public bool IsReported(SyntaxRule rule)
    {
        return Reporter.IsReported(rule);
    }
}

/// <summary>
/// Reads the notes of one timing point into <see cref="ParsedNote"/> values.
/// </summary>
/// <remarks>
/// <para>
/// The text of a timing point is split by <c>/</c> into notes; a note with <c>*</c> is a same head group whose later
/// parts start from the button of the first part. Two digits such as <c>18</c> are two taps.
/// </para>
/// <para>
/// These are always errors (<see cref="SyntaxRuleKind.Invalid"/>): a note with nothing left after removing its
/// markers, an empty <c>*</c> part, a touch area letter that is not at the start of a note, a duration with too many
/// separators or with text between the two <c>#</c> of <c>[wait##length]</c>, and a hold whose <c>]</c> comes before
/// its <c>[</c>.
/// </para>
/// <para>
/// In lenient mode a note that cannot be read is skipped, as are empty <c>/</c> parts, an unreadable hold duration
/// becomes 0, and a <c>b</c> or <c>m</c> in the middle of a slide path is ignored. Strict mode throws for all of these,
/// and also checks the shape of taps, holds and touches and the path of slides (see <see cref="SlidePathChecker"/>).
/// </para>
/// </remarks>
internal static class NoteReader
{
    /// <summary>
    /// The longest note text that is copied on the stack instead of a heap array.
    /// </summary>
    private const int StackTextLimit = 256;

    /// <summary>
    /// The result of reading one note.
    /// </summary>
    private enum ReadOutcome
    {
        /// <summary>The note was read.</summary>
        Read,
        /// <summary>The text is not a note that can be read; the caller reports it.</summary>
        Unreadable,
        /// <summary>The note breaks the grammar and was already reported; it is skipped.</summary>
        Rejected
    }

    /// <summary>
    /// Reads the notes of a timing point.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="output">Receives the notes in text order.</param>
    /// <exception cref="InvalidSimaiNoteException">Thrown when a note breaks the grammar.</exception>
    public static void ReadTimingPoint(in NoteSource source, PooledBuffer<ParsedNote> output)
    {
        var content = source.Content;
        if (content.IsEmpty)
        {
            return;
        }
        if (content.Length == 2 && IsDigit(content[0]) && IsDigit(content[1]))
        {
            // Two digits are two taps: "18" is "1/8".
            ReadNote(source, content.Slice(0, 1), output);
            ReadNote(source, content.Slice(1, 1), output);
            return;
        }
        var rest = content;
        while (true)
        {
            var separator = rest.IndexOf('/');
            var note = separator == -1 ? rest : rest.Slice(0, separator);
            if (note.IsEmpty)
            {
                if (source.IsReported(SyntaxRules.EmptyEachPart))
                {
                    source.Report(SyntaxRules.EmptyEachPart, "The timing point has an empty note between '/'.", note);
                }
            }
            else if (note.IndexOf('*') != -1)
            {
                ReadSameHeadGroup(source, note, output);
            }
            else
            {
                ReadNote(source, note, output);
            }
            if (separator == -1)
            {
                return;
            }
            rest = rest.Slice(separator + 1);
        }
    }

    /// <summary>
    /// Reads a note that is not part of a <c>*</c> group.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="note">The note text.</param>
    /// <param name="output">Receives the note.</param>
    private static void ReadNote(in NoteSource source, ReadOnlySpan<char> note, PooledBuffer<ParsedNote> output)
    {
        if (ReadSingle(source, note, SameHeadRole.None, output) == ReadOutcome.Unreadable && source.IsReported(SyntaxRules.UnreadableNote))
        {
            source.Report(SyntaxRules.UnreadableNote, $"Cannot read the note \"{note.ToString()}\".", note);
        }
    }

    /// <summary>
    /// Reads a same head group such as <c>1-3[8:1]*-5[8:1]</c>. Every later part is read with the first character of
    /// the first part in front of it.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="group">The text of the group.</param>
    /// <param name="output">Receives the notes.</param>
    /// <exception cref="InvalidSimaiNoteException">Thrown when a part is empty or breaks the grammar.</exception>
    private static void ReadSameHeadGroup(in NoteSource source, ReadOnlySpan<char> group, PooledBuffer<ParsedNote> output)
    {
        var rest = group;
        while (true)
        {
            var separator = rest.IndexOf('*');
            var part = separator == -1 ? rest : rest.Slice(0, separator);
            if (part.IsEmpty)
            {
                source.Report(SyntaxRules.EmptySameHeadPart, $"The same head group \"{group.ToString()}\" has an empty part.", group);
                return;
            }
            if (separator == -1)
            {
                break;
            }
            rest = rest.Slice(separator + 1);
        }

        var firstLength = group.IndexOf('*');
        var first = group.Slice(0, firstLength);
        var firstOutcome = ReadSingle(source, first, SameHeadRole.First, output);
        if (firstOutcome != ReadOutcome.Read)
        {
            if (firstOutcome == ReadOutcome.Unreadable && source.IsReported(SyntaxRules.UnreadableNote))
            {
                source.Report(SyntaxRules.UnreadableNote, $"Cannot read the slide \"{first.ToString()}\"; the same head group \"{group.ToString()}\" is skipped.", first);
            }
            return;
        }
        // A part is never longer than the group, so one buffer fits every part with the head button in front.
        var bufferLength = group.Length + 1;
        var heapText = bufferLength > StackTextLimit ? new char[bufferLength] : null;
        var buffer = heapText is null ? stackalloc char[bufferLength] : heapText.AsSpan();
        buffer[0] = first[0];
        rest = group.Slice(firstLength + 1);
        while (true)
        {
            var separator = rest.IndexOf('*');
            var part = separator == -1 ? rest : rest.Slice(0, separator);
            part.CopyTo(buffer.Slice(1));
            var text = buffer.Slice(0, part.Length + 1);
            if (ReadSingle(source, text, SameHeadRole.Continuation, output) == ReadOutcome.Unreadable && source.IsReported(SyntaxRules.UnreadableNote))
            {
                source.Report(SyntaxRules.UnreadableNote, $"Cannot read the slide \"{text.ToString()}\" of the same head group \"{group.ToString()}\".", text);
            }
            if (separator == -1)
            {
                return;
            }
            rest = rest.Slice(separator + 1);
        }
    }

    /// <summary>
    /// Reads a single note and appends it to <paramref name="output"/> when it could be read.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="noteText">The note text with markers.</param>
    /// <param name="role">How the note relates to the <c>*</c> syntax.</param>
    /// <param name="output">Receives the note.</param>
    /// <returns>Whether the note was read, could not be read, or was rejected and already reported.</returns>
    /// <exception cref="InvalidSimaiNoteException">Thrown when the note breaks the grammar.</exception>
    private static ReadOutcome ReadSingle(in NoteSource source, ReadOnlySpan<char> noteText, SameHeadRole role, PooledBuffer<ParsedNote> output)
    {
        var heapText = noteText.Length > StackTextLimit ? new char[noteText.Length] : null;
        var buffer = heapText is null ? stackalloc char[noteText.Length] : heapText.AsSpan();
        var markers = NoteMarkers.Detect(noteText, buffer);
        var text = buffer.Slice(0, markers.TextLength);
        if (markers.HasMisplacedTouchLetter)
        {
            source.Report(SyntaxRules.MisplacedTouchArea, $"The note \"{noteText.ToString()}\" has a touch area letter that is not at its start.", noteText);
            return ReadOutcome.Rejected;
        }
        if (text.IsEmpty)
        {
            source.Report(SyntaxRules.EmptyNote, $"The note \"{noteText.ToString()}\" has no button or sensor.", noteText);
            return ReadOutcome.Rejected;
        }
        if (markers.IgnoredMarker != '\0' && source.IsReported(SyntaxRules.IgnoredMarker))
        {
            source.Report(SyntaxRules.IgnoredMarker, $"The '{markers.IgnoredMarker}' in \"{noteText.ToString()}\" has no effect; write it right before '[' or at the end of the slide.", noteText);
        }
        var note = new ParsedNote
        {
            TouchArea = ' ',
            Role = role,
        };
        if (markers.IsTouch)
        {
            note.TouchArea = text[0];
            if (note.TouchArea != 'C')
            {
                if (text.Length < 2 || !DurationReader.TryParseInt(text.Slice(1, 1), out note.StartPosition))
                {
                    return ReadOutcome.Unreadable;
                }
            }
            else
            {
                note.StartPosition = 8;
            }
            note.Type = SimaiNoteType.Touch;
        }
        else
        {
            if (!DurationReader.TryParseInt(text.Slice(0, 1), out note.StartPosition))
            {
                return ReadOutcome.Unreadable;
            }
            note.Type = SimaiNoteType.Tap;
        }

        if (markers.IsHold)
        {
            note.Type = markers.IsTouch ? SimaiNoteType.TouchHold : SimaiNoteType.Hold;
            // "1h" is a hold without a length; a touch hold always looks for one.
            if (markers.IsTouch || text[text.Length - 1] != 'h')
            {
                var status = DurationReader.ReadHoldTime(source.Bpm, text, out note.HoldTime);
                if (ReportMalformed(source, status, noteText))
                {
                    return ReadOutcome.Rejected;
                }
                if (status == DurationStatus.Unreadable && source.IsReported(SyntaxRules.UnreadableHoldLength))
                {
                    source.Report(SyntaxRules.UnreadableHoldLength, $"Cannot read the length of the hold \"{noteText.ToString()}\"; it is 0.", noteText);
                }
            }
        }

        var flags = markers.Flags;
        if (markers.IsSlide)
        {
            note.Type = SimaiNoteType.Slide;
            var status = DurationReader.ReadSlideTimes(source.Bpm, text, out var waitTime, out var slideTime);
            if (ReportMalformed(source, status, noteText))
            {
                return ReadOutcome.Rejected;
            }
            if (status != DurationStatus.Success)
            {
                return ReadOutcome.Unreadable;
            }
            note.SlideTime = slideTime;
            note.SlideStartTime = source.Timing + waitTime;
            note.HeadLength = markers.HeadLength;
            if (markers.HasNoHeadMarker)
            {
                flags |= ParsedNoteFlags.NoHead;
            }
        }
        if (source.Reporter.ChecksStrictly && !IsShapeValid(source, note.Type, text, markers.HeadLength, noteText))
        {
            return ReadOutcome.Rejected;
        }
        if (role == SameHeadRole.Continuation)
        {
            flags |= ParsedNoteFlags.NoHead;
        }
        note.Flags = flags;
        note.RawContent = StringCache.Get(text);
        output.Add(note);

        return ReadOutcome.Read;
    }

    /// <summary>
    /// Reports a duration that is malformed in a way that is always an error.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="status">The result of reading the duration.</param>
    /// <param name="noteText">The note text.</param>
    /// <returns><see langword="true"/> when the duration was malformed and the note must be skipped.</returns>
    /// <exception cref="InvalidSimaiNoteException">Thrown when <paramref name="status"/> is malformed and the reporter does not collect.</exception>
    private static bool ReportMalformed(in NoteSource source, DurationStatus status, ReadOnlySpan<char> noteText)
    {
        var problem = status switch
        {
            DurationStatus.ReversedBrackets => "has ']' before '['",
            DurationStatus.TooManySeparators => "has a duration with too many ':' or '#'",
            DurationStatus.UnexpectedPart => "has text between the '#' of a [wait##length] duration",
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }
        source.Report(SyntaxRules.MalformedDuration, $"The note \"{noteText.ToString()}\" {problem}.", noteText);

        return true;
    }

    /// <summary>
    /// Checks the shape of a note in strict mode: buttons are 1 to 8, sensors are <c>A1</c> to <c>E8</c> or <c>C</c>,
    /// <c>C1</c>, <c>C2</c>, a hold is a button or sensor followed by <c>h</c> and an optional duration, <c>f</c> only
    /// appears on touches, and a slide path is checked by <see cref="SlidePathChecker"/>.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="type">The kind of the note.</param>
    /// <param name="text">The note text without markers.</param>
    /// <param name="headLength">For a slide, the length of the text before the first slide mark.</param>
    /// <param name="noteText">The note text with markers.</param>
    /// <returns><see langword="true"/> when the shape is valid, or when the problem only needs a report.</returns>
    /// <exception cref="InvalidSimaiNoteException">Thrown when the shape is wrong and the reporter does not collect.</exception>
    private static bool IsShapeValid(in NoteSource source, SimaiNoteType type, ReadOnlySpan<char> text, int headLength, ReadOnlySpan<char> noteText)
    {
        if (type == SimaiNoteType.Slide)
        {
            if (!IsButton(text[0]) || headLength != 1)
            {
                source.Report(SyntaxRules.InvalidNoteShape, $"The slide \"{noteText.ToString()}\" must start with a button from 1 to 8.", noteText);
                return false;
            }

            return SlidePathChecker.Check(source, text, noteText);
        }
        var isValid = type switch
        {
            SimaiNoteType.Tap => text.Length == 1 && IsButton(text[0]),
            SimaiNoteType.Hold => text.Length >= 2 && IsButton(text[0]) && text[1] == 'h' && IsOptionalDuration(text.Slice(2)),
            SimaiNoteType.Touch => TryReadSensor(text, out var touchLength) && IsTouchSuffix(text.Slice(touchLength), false),
            _ => TryReadSensor(text, out var holdLength) && IsTouchSuffix(text.Slice(holdLength), true),
        };
        if (!isValid)
        {
            source.Report(SyntaxRules.InvalidNoteShape, $"The note \"{noteText.ToString()}\" is not a valid {type}.", noteText);
        }

        return isValid;
    }

    /// <summary>
    /// Reads the sensor at the start of a touch: an area letter and a number from 1 to 8, or <c>C</c> with an
    /// optional 1 or 2.
    /// </summary>
    /// <param name="text">The note text without markers.</param>
    /// <param name="length">Receives the length of the sensor text.</param>
    /// <returns><see langword="true"/> when the text starts with a sensor.</returns>
    private static bool TryReadSensor(ReadOnlySpan<char> text, out int length)
    {
        length = 0;
        if (text[0] == 'C')
        {
            length = text.Length > 1 && (text[1] == '1' || text[1] == '2') ? 2 : 1;
            return true;
        }
        if (text.Length < 2 || !IsButton(text[1]))
        {
            return false;
        }
        length = 2;

        return true;
    }

    /// <summary>
    /// Checks what follows the sensor of a touch: at most one <c>f</c>, and for a touch hold one <c>h</c> in any order
    /// with the <c>f</c>, followed by an optional duration.
    /// </summary>
    /// <param name="suffix">The text after the sensor.</param>
    /// <param name="isHold">Whether the note is a touch hold.</param>
    /// <returns><see langword="true"/> when the suffix is valid.</returns>
    private static bool IsTouchSuffix(ReadOnlySpan<char> suffix, bool isHold)
    {
        var hasHanabi = false;
        var hasHold = false;
        var i = 0;
        for (; i < suffix.Length && suffix[i] != '['; i++)
        {
            switch (suffix[i])
            {
                case 'f' when !hasHanabi:
                    hasHanabi = true;
                    break;
                case 'h' when isHold && !hasHold:
                    hasHold = true;
                    break;
                default:
                    return false;
            }
        }
        if (isHold != hasHold)
        {
            return false;
        }

        return isHold ? IsOptionalDuration(suffix.Slice(i)) : i == suffix.Length;
    }

    /// <summary>
    /// Checks that a text is empty or a single bracket <c>[...]</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text is empty or one bracket.</returns>
    private static bool IsOptionalDuration(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return true;
        }

        return text[0] == '[' && text.IndexOf(']') == text.Length - 1;
    }

    /// <summary>
    /// Gets whether a character is a button from 1 to 8.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for 1 to 8.</returns>
    private static bool IsButton(char c)
    {
        return c >= '1' && c <= '8';
    }

    /// <summary>
    /// Gets whether a character is a decimal digit.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for 0 to 9.</returns>
    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    /// <summary>
    /// The markers of a note text and the text without them.
    /// </summary>
    private readonly ref struct NoteMarkers
    {
        /// <summary>
        /// Creates the markers of a note text.
        /// </summary>
        /// <param name="flags">The markers that become note flags.</param>
        /// <param name="isTouch">Whether the note is a touch.</param>
        /// <param name="isHold">Whether the note is a hold.</param>
        /// <param name="isSlide">Whether the note is a slide.</param>
        /// <param name="hasNoHeadMarker">Whether the text has <c>!</c> or <c>?</c>.</param>
        /// <param name="hasMisplacedTouchLetter">Whether a touch area letter appears after the start of the note.</param>
        /// <param name="ignoredMarker">The first <c>b</c> or <c>m</c> that has no effect, or <c>'\0'</c>.</param>
        /// <param name="headLength">The length of the star head text.</param>
        /// <param name="textLength">The length of the text without markers.</param>
        private NoteMarkers(ParsedNoteFlags flags,
                            bool isTouch,
                            bool isHold,
                            bool isSlide,
                            bool hasNoHeadMarker,
                            bool hasMisplacedTouchLetter,
                            char ignoredMarker,
                            int headLength,
                            int textLength)
        {
            Flags = flags;
            IsTouch = isTouch;
            IsHold = isHold;
            IsSlide = isSlide;
            HasNoHeadMarker = hasNoHeadMarker;
            HasMisplacedTouchLetter = hasMisplacedTouchLetter;
            IgnoredMarker = ignoredMarker;
            HeadLength = headLength;
            TextLength = textLength;
        }

        /// <summary>Gets the markers that become note flags.</summary>
        public ParsedNoteFlags Flags { get; }

        /// <summary>Gets whether the text starts with a touch area letter and has no slide mark.</summary>
        public bool IsTouch { get; }

        /// <summary>Gets whether the text has <c>h</c> and no slide mark.</summary>
        public bool IsHold { get; }

        /// <summary>Gets whether the text has a slide mark or a Slide Code path letter.</summary>
        public bool IsSlide { get; }

        /// <summary>Gets whether the text has <c>!</c> or <c>?</c>.</summary>
        public bool HasNoHeadMarker { get; }

        /// <summary>Gets whether a touch area letter appears after the start of the note.</summary>
        public bool HasMisplacedTouchLetter { get; }

        /// <summary>Gets the first <c>b</c> or <c>m</c> that has no effect, or <c>'\0'</c>.</summary>
        public char IgnoredMarker { get; }

        /// <summary>Gets the length of the text without markers before the first slide mark.</summary>
        public int HeadLength { get; }

        /// <summary>Gets the length of the text without markers.</summary>
        public int TextLength { get; }

        /// <summary>
        /// Finds the markers of a note text and copies the text without the removed markers into
        /// <paramref name="destination"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A touch starts with an area letter <c>A</c> to <c>E</c>. <c>K</c> is a slide mark in any note. In a note that
        /// starts with a button and has a <c>K</c> (Slide Code always ends with <c>K</c>), the letters <c>A</c>,
        /// <c>B</c>, <c>C</c>, <c>P</c> and <c>Q</c> are Slide Code path letters. Otherwise an area letter after the
        /// start of a note is misplaced, and <c>P</c> and <c>Q</c> are plain text as in MajSimai 2.x.
        /// </para>
        /// <para>
        /// <c>b</c> and <c>m</c> belong to the star head when they come before the slide path. After a slide mark they
        /// mark the slide path only when they are the last character or are followed by <c>[</c>; otherwise they have
        /// no effect.
        /// </para>
        /// </remarks>
        /// <param name="noteText">The note text.</param>
        /// <param name="destination">Receives the text without markers; must be at least as long as the note text.</param>
        /// <returns>The markers.</returns>
        public static NoteMarkers Detect(ReadOnlySpan<char> noteText, Span<char> destination)
        {
            var flags = ParsedNoteFlags.UsingSV;
            var isTouch = false;
            var isHold = false;
            var isSlide = false;
            var hasNoHeadMarker = false;
            var hasMisplacedTouchLetter = false;
            var ignoredMarker = '\0';
            var headLength = 0;
            var forceStarCount = 0;
            var length = 0;
            var hasSlideCodeEnd = noteText.IndexOf('K') != -1;
            for (var i = 0; i < noteText.Length; i++)
            {
                var c = noteText[i];
                var isSlideCodeLetter = hasSlideCodeEnd && length > 0 && IsDigit(destination[0]);
                switch (c)
                {
                    case '-':
                    case '^':
                    case 'v':
                    case '<':
                    case '>':
                    case 'V':
                    case 'p':
                    case 'q':
                    case 's':
                    case 'z':
                    case 'w':
                    case 'K':
                        MarkSlide(ref isSlide, ref headLength, length);
                        break;
                    case 'A':
                    case 'B':
                    case 'C':
                        if (length == 0)
                        {
                            isTouch = true;
                        }
                        else if (isSlideCodeLetter)
                        {
                            MarkSlide(ref isSlide, ref headLength, length);
                        }
                        else
                        {
                            hasMisplacedTouchLetter = true;
                        }
                        break;
                    case 'D':
                    case 'E':
                        if (length == 0)
                        {
                            isTouch = true;
                        }
                        else
                        {
                            hasMisplacedTouchLetter = true;
                        }
                        break;
                    case 'P':
                    case 'Q':
                        if (isSlideCodeLetter)
                        {
                            MarkSlide(ref isSlide, ref headLength, length);
                        }
                        break;
                    case 'f':
                        flags |= ParsedNoteFlags.Hanabi;
                        break;
                    case 'h':
                        isHold = true;
                        break;
                    case 'x':
                        flags |= ParsedNoteFlags.Ex;
                        continue;
                    case 'c':
                        flags &= ~ParsedNoteFlags.UsingSV;
                        continue;
                    case '!':
                    case '?':
                        hasNoHeadMarker = true;
                        continue;
                    case '@':
                        flags |= ParsedNoteFlags.TapHeadSlide;
                        continue;
                    case '$':
                        forceStarCount++;
                        continue;
                    case 'b':
                        flags = ReadHeadOrPathMarker(noteText, i, isSlide, flags, ParsedNoteFlags.Break, ParsedNoteFlags.SlideBreak, ref ignoredMarker);
                        continue;
                    case 'm':
                        flags = ReadHeadOrPathMarker(noteText, i, isSlide, flags, ParsedNoteFlags.Mine, ParsedNoteFlags.MineSlide, ref ignoredMarker);
                        continue;
                }
                destination[length++] = c;
            }
            if (forceStarCount > 0)
            {
                flags |= ParsedNoteFlags.ForceStar;
            }
            if (forceStarCount > 1)
            {
                flags |= ParsedNoteFlags.FakeRotate;
            }
            if (isSlide)
            {
                isHold = false;
                isTouch = false;
            }

            return new NoteMarkers(flags,
                                   isTouch,
                                   isHold,
                                   isSlide,
                                   hasNoHeadMarker,
                                   hasMisplacedTouchLetter,
                                   ignoredMarker,
                                   headLength,
                                   length);
        }

        /// <summary>
        /// Marks the note as a slide, and remembers where the slide path starts the first time.
        /// </summary>
        /// <param name="isSlide">Whether the note is a slide.</param>
        /// <param name="headLength">The length of the star head text.</param>
        /// <param name="length">The length of the text without markers so far.</param>
        private static void MarkSlide(ref bool isSlide, ref int headLength, int length)
        {
            if (!isSlide)
            {
                headLength = length;
            }
            isSlide = true;
        }

        /// <summary>
        /// Applies a <c>b</c> or <c>m</c> marker to either the star head or the slide path.
        /// </summary>
        /// <param name="noteText">The note text.</param>
        /// <param name="index">The index of the marker.</param>
        /// <param name="isSlide">Whether a slide mark came before the marker.</param>
        /// <param name="flags">The flags so far.</param>
        /// <param name="headFlag">The flag of the marker on the head.</param>
        /// <param name="pathFlag">The flag of the marker on the slide path.</param>
        /// <param name="ignoredMarker">Receives the marker when it has no effect and no earlier marker had none.</param>
        /// <returns>The flags with the marker applied.</returns>
        private static ParsedNoteFlags ReadHeadOrPathMarker(ReadOnlySpan<char> noteText,
                                                            int index,
                                                            bool isSlide,
                                                            ParsedNoteFlags flags,
                                                            ParsedNoteFlags headFlag,
                                                            ParsedNoteFlags pathFlag,
                                                            ref char ignoredMarker)
        {
            if ((flags & headFlag) != 0 && (flags & pathFlag) != 0)
            {
                return flags;
            }
            if (!isSlide)
            {
                return flags | headFlag;
            }
            // 1-3b[8:1] marks the path, and so does a marker at the very end: 1-3[8:1]b.
            var isLast = index == noteText.Length - 1;
            if (isLast || noteText[index + 1] == '[')
            {
                return flags | pathFlag;
            }
            if (ignoredMarker == '\0')
            {
                ignoredMarker = noteText[index];
            }

            return flags;
        }
    }
}
