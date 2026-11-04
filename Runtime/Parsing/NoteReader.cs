using System;
using MajSimai.Logging;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Reads the notes of one timing point into <see cref="ParsedNote"/> values, following the note grammar of MajSimai 2.x
/// exactly, including its quirks.
/// </summary>
/// <remarks>
/// The text of a timing point is split by <c>/</c> into groups; a group with <c>*</c> is a same head group. Two cases
/// stop the reading of the whole timing point and keep only the notes read before, as 2.x did when it threw: a note whose
/// text is empty after removing markers, and a hold whose first <c>]</c> comes before its first <c>[</c>.
/// </remarks>
internal static class NoteReader
{
    /// <summary>
    /// The longest note text that is copied on the stack instead of a rented array.
    /// </summary>
    private const int StackTextLimit = 256;

    /// <summary>
    /// The result of reading one note.
    /// </summary>
    private enum ReadResult
    {
        /// <summary>The note was read.</summary>
        Success,
        /// <summary>The text is not a note; reading continues with the next note.</summary>
        Failed,
        /// <summary>The rest of the timing point must be dropped.</summary>
        Abort
    }

    /// <summary>
    /// Reads the notes of a timing point.
    /// </summary>
    /// <param name="timing">The time of the timing point in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="content">The note text of the timing point, without whitespace.</param>
    /// <param name="output">Receives the notes in text order.</param>
    public static void ReadTimingPoint(double timing, double bpm, ReadOnlySpan<char> content, PooledBuffer<ParsedNote> output)
    {
        if (content.IsEmpty)
        {
            return;
        }
        if (content.Length == 2 && DurationReader.TryParseInt(content, out _))
        {
            // Two digits are two taps: "18" is "1/8".
            ReadSingle(timing, bpm, content.Slice(0, 1), SameHeadRole.None, output);
            ReadSingle(timing, bpm, content.Slice(1, 1), SameHeadRole.None, output);
            return;
        }
        var rest = content;
        while (true)
        {
            var separator = rest.IndexOf('/');
            var group = separator == -1 ? rest : rest.Slice(0, separator);
            if (!group.IsEmpty && ReadGroup(timing, bpm, group, output) == ReadResult.Abort)
            {
                if (SimaiLogger.IsEnabled(SimaiLogLevel.Debug))
                {
                    SimaiLogger.Debug($"Stopped reading timing point \"{content.ToString()}\" at \"{group.ToString()}\"; the remaining notes are dropped.");
                }
                return;
            }
            if (separator == -1)
            {
                return;
            }
            rest = rest.Slice(separator + 1);
        }
    }

    /// <summary>
    /// Reads one <c>/</c> separated group.
    /// </summary>
    /// <param name="timing">The time of the timing point in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="group">The text of the group.</param>
    /// <param name="output">Receives the notes.</param>
    /// <returns>Whether the timing point must stop being read.</returns>
    private static ReadResult ReadGroup(double timing, double bpm, ReadOnlySpan<char> group, PooledBuffer<ParsedNote> output)
    {
        if (group.IndexOf('*') != -1)
        {
            return ReadSameHeadGroup(timing, bpm, group, output);
        }
        var result = ReadSingle(timing, bpm, group, SameHeadRole.None, output);
        if (result == ReadResult.Failed)
        {
            LogUnreadable("note", group);
        }

        return result;
    }

    /// <summary>
    /// Reads a same head group such as <c>1-3[8:1]*-5[8:1]</c>. Every later part is read with the first character of
    /// the first part in front of it.
    /// </summary>
    /// <remarks>
    /// The parts are found with the 2.x split, which skips empty parts but still visits as many parts as there are
    /// <c>*</c> plus one. A visited part that the split did not fill is empty, so a trailing <c>*</c> reads an extra tap
    /// on the head button.
    /// </remarks>
    /// <param name="timing">The time of the timing point in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="group">The text of the group.</param>
    /// <param name="output">Receives the notes.</param>
    /// <returns>Whether the timing point must stop being read.</returns>
    private static ReadResult ReadSameHeadGroup(double timing, double bpm, ReadOnlySpan<char> group, PooledBuffer<ParsedNote> output)
    {
        var partCount = LegacySplit.Count(group, '*') + 1;
        var heapRanges = partCount > 16 ? new Range[partCount] : null;
        var ranges = heapRanges is null ? stackalloc Range[partCount] : heapRanges.AsSpan();
        ranges.Clear();
        LegacySplit.Split(group, ranges, '*', true);

        var first = group[ranges[0]];
        var result = ReadSingle(timing, bpm, first, SameHeadRole.First, output);
        if (result != ReadResult.Success)
        {
            if (result == ReadResult.Failed)
            {
                LogUnreadable("slide", first);
            }
            return result;
        }
        // A part is never longer than the group, so one buffer fits every part with the head button in front.
        var bufferLength = group.Length + 1;
        var heapText = bufferLength > StackTextLimit ? new char[bufferLength] : null;
        var buffer = heapText is null ? stackalloc char[bufferLength] : heapText.AsSpan();
        buffer[0] = first[0];
        for (var i = 1; i < ranges.Length; i++)
        {
            var part = group[ranges[i]];
            part.CopyTo(buffer.Slice(1));
            var text = buffer.Slice(0, part.Length + 1);
            result = ReadSingle(timing, bpm, text, SameHeadRole.Continuation, output);
            if (result == ReadResult.Abort)
            {
                return result;
            }
            if (result == ReadResult.Failed)
            {
                LogUnreadable("slide", text);
            }
        }

        return ReadResult.Success;
    }

    /// <summary>
    /// Reads a single note and appends it to <paramref name="output"/> when it could be read.
    /// </summary>
    /// <param name="timing">The time of the timing point in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="noteText">The note text with markers.</param>
    /// <param name="role">How the note relates to the <c>*</c> syntax.</param>
    /// <param name="output">Receives the note.</param>
    /// <returns>Whether the note was read, could not be read, or stops the timing point.</returns>
    private static ReadResult ReadSingle(double timing,
                                         double bpm,
                                         ReadOnlySpan<char> noteText,
                                         SameHeadRole role,
                                         PooledBuffer<ParsedNote> output)
    {
        var heapText = noteText.Length > StackTextLimit ? new char[noteText.Length] : null;
        var buffer = heapText is null ? stackalloc char[noteText.Length] : heapText.AsSpan();
        var markers = NoteMarkers.Detect(noteText, buffer);
        var text = buffer.Slice(0, markers.TextLength);
        if (text.IsEmpty)
        {
            return ReadResult.Abort;
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
                    return ReadResult.Failed;
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
                return ReadResult.Failed;
            }
            note.Type = SimaiNoteType.Tap;
        }

        if (markers.IsHold)
        {
            if (DurationReader.HasReversedHoldBrackets(text) && (markers.IsTouch || text[text.Length - 1] != 'h'))
            {
                return ReadResult.Abort;
            }
            if (markers.IsTouch)
            {
                note.Type = SimaiNoteType.TouchHold;
                DurationReader.TryReadHoldTime(bpm, text, out note.HoldTime);
            }
            else
            {
                note.Type = SimaiNoteType.Hold;
                if (text[text.Length - 1] != 'h')
                {
                    DurationReader.TryReadHoldTime(bpm, text, out note.HoldTime);
                }
            }
        }

        var flags = markers.Flags;
        if (markers.IsSlide)
        {
            note.Type = SimaiNoteType.Slide;
            if (!DurationReader.TryReadSlideTimes(bpm, text, out var waitTime, out var slideTime))
            {
                return ReadResult.Failed;
            }
            note.SlideTime = slideTime;
            note.SlideStartTime = timing + waitTime;
            note.HeadLength = markers.HeadLength;
            if (markers.HasNoHeadMarker)
            {
                flags |= ParsedNoteFlags.NoHead;
            }
        }
        if (role == SameHeadRole.Continuation)
        {
            flags |= ParsedNoteFlags.NoHead;
        }
        note.Flags = flags;
        note.RawContent = StringCache.Get(((ReadOnlySpan<char>)text).Trim());
        output.Add(note);

        return ReadResult.Success;
    }

    /// <summary>
    /// Logs a note text that could not be read, at the debug level.
    /// </summary>
    /// <param name="kind">What the text was read as.</param>
    /// <param name="text">The text.</param>
    private static void LogUnreadable(string kind, ReadOnlySpan<char> text)
    {
        if (SimaiLogger.IsEnabled(SimaiLogLevel.Debug))
        {
            SimaiLogger.Debug($"Cannot parse {kind} from text: \"{text.ToString()}\"");
        }
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
        /// <param name="headLength">The length of the star head text.</param>
        /// <param name="textLength">The length of the text without markers.</param>
        private NoteMarkers(ParsedNoteFlags flags,
                            bool isTouch,
                            bool isHold,
                            bool isSlide,
                            bool hasNoHeadMarker,
                            int headLength,
                            int textLength)
        {
            Flags = flags;
            IsTouch = isTouch;
            IsHold = isHold;
            IsSlide = isSlide;
            HasNoHeadMarker = hasNoHeadMarker;
            HeadLength = headLength;
            TextLength = textLength;
        }

        /// <summary>Gets the markers that become note flags.</summary>
        public ParsedNoteFlags Flags { get; }

        /// <summary>Gets whether the text has a touch area letter and no slide mark.</summary>
        public bool IsTouch { get; }

        /// <summary>Gets whether the text has <c>h</c> and no slide mark.</summary>
        public bool IsHold { get; }

        /// <summary>Gets whether the text has a slide mark.</summary>
        public bool IsSlide { get; }

        /// <summary>Gets whether the text has <c>!</c> or <c>?</c>.</summary>
        public bool HasNoHeadMarker { get; }

        /// <summary>Gets the length of the text without markers before the first slide mark.</summary>
        public int HeadLength { get; }

        /// <summary>Gets the length of the text without markers.</summary>
        public int TextLength { get; }

        /// <summary>
        /// Finds the markers of a note text and copies the text without the removed markers into
        /// <paramref name="destination"/>.
        /// </summary>
        /// <remarks>
        /// <c>b</c> and <c>m</c> belong to the star head when they come before the first slide mark. After a slide mark
        /// they mark the slide path only when they are the last character or are followed by <c>[</c>; otherwise they are
        /// ignored.
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
            var headLength = 0;
            var forceStarCount = 0;
            var length = 0;
            for (var i = 0; i < noteText.Length; i++)
            {
                var c = noteText[i];
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
                        if (!isSlide)
                        {
                            headLength = length;
                        }
                        isSlide = true;
                        break;
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                        isTouch = true;
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
                        flags = ReadHeadOrPathMarker(noteText, i, isSlide, flags, ParsedNoteFlags.Break, ParsedNoteFlags.SlideBreak);
                        continue;
                    case 'm':
                        flags = ReadHeadOrPathMarker(noteText, i, isSlide, flags, ParsedNoteFlags.Mine, ParsedNoteFlags.MineSlide);
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

            return new NoteMarkers(flags, isTouch, isHold, isSlide, hasNoHeadMarker, headLength, length);
        }

        /// <summary>
        /// Applies a <c>b</c> or <c>m</c> marker to either the star head or the slide path.
        /// </summary>
        private static ParsedNoteFlags ReadHeadOrPathMarker(ReadOnlySpan<char> noteText,
                                                            int index,
                                                            bool isSlide,
                                                            ParsedNoteFlags flags,
                                                            ParsedNoteFlags headFlag,
                                                            ParsedNoteFlags pathFlag)
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

            return flags;
        }
    }
}
