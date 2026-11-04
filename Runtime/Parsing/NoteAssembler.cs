using System;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Turns the <see cref="ParsedNote"/> values of a timing point into <see cref="SimaiNote"/> objects.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A slide with a star head becomes a <see cref="SimaiNoteType.Tap"/> head placed right before a
/// <see cref="SimaiNoteType.Slide"/> note. When the slide is the first part of a <c>*</c> group, the later slide parts
/// also belong to the head. The head lists its slides in <see cref="SimaiNote.RelatedNotes"/>.</item>
/// <item>A slide without a head stays a single <see cref="SimaiNoteType.Slide"/> note.</item>
/// <item><see cref="SimaiNote.IsEach"/>, <see cref="SimaiNote.IsStar"/> and <see cref="SimaiNote.IsStarDouble"/> are
/// computed once every note of the timing point exists.</item>
/// </list>
/// </remarks>
internal static class NoteAssembler
{
    /// <summary>
    /// The flags a star head takes from a slide written with its head.
    /// </summary>
    private const ParsedNoteFlags HeadFlags = ParsedNoteFlags.Break |
                                              ParsedNoteFlags.Ex |
                                              ParsedNoteFlags.Mine |
                                              ParsedNoteFlags.ForceStar |
                                              ParsedNoteFlags.FakeRotate |
                                              ParsedNoteFlags.TapHeadSlide |
                                              ParsedNoteFlags.Hanabi |
                                              ParsedNoteFlags.UsingSV;

    /// <summary>
    /// Creates the notes of a timing point.
    /// </summary>
    /// <param name="parsedNotes">The notes read from the text, in text order.</param>
    /// <param name="workspace">The buffers used while building the notes.</param>
    /// <returns>The notes, or an empty array when there are none.</returns>
    public static SimaiNote[] Assemble(ReadOnlySpan<ParsedNote> parsedNotes, NoteWorkspace workspace)
    {
        if (parsedNotes.IsEmpty)
        {
            return Array.Empty<SimaiNote>();
        }
        var notes = workspace.Notes;
        var relatedSlides = workspace.RelatedSlides;
        notes.Clear();
        relatedSlides.Clear();
        var head = default(SimaiNote);
        for (var i = 0; i < parsedNotes.Length; i++)
        {
            ref readonly var parsed = ref parsedNotes[i];
            var isSlide = parsed.Type == SimaiNoteType.Slide;
            if (head is not null && parsed.Role == SameHeadRole.Continuation)
            {
                // A later part of the same * group as the open head.
                var continuation = isSlide ? CreateSlide(in parsed) : CreateNote(in parsed);
                if (isSlide)
                {
                    relatedSlides.Add(continuation);
                }
                notes.Add(continuation);
                continue;
            }
            CloseHead(ref head, relatedSlides);
            if (isSlide && !parsed.Has(ParsedNoteFlags.NoHead))
            {
                var slide = CreateSlide(in parsed);
                head = CreateHead(in parsed);
                relatedSlides.Add(slide);
                notes.Add(head);
                notes.Add(slide);
            }
            else if (isSlide)
            {
                notes.Add(CreateSlide(in parsed));
            }
            else
            {
                notes.Add(CreateNote(in parsed));
            }
        }
        CloseHead(ref head, relatedSlides);
        var result = notes.ToArray();
        notes.Clear();
        ApplyGroupFlags(result);

        return result;
    }

    /// <summary>
    /// Gives the open star head the slides collected for it.
    /// </summary>
    private static void CloseHead(ref SimaiNote? head, PooledBuffer<SimaiNote> relatedSlides)
    {
        if (head is null)
        {
            return;
        }
        head.RelatedNotes = relatedSlides.ToArray();
        relatedSlides.Clear();
        head = null;
    }

    /// <summary>
    /// Sets the flags that depend on the other notes of the timing point.
    /// </summary>
    /// <param name="notes">Every note of the timing point.</param>
    private static void ApplyGroupFlags(SimaiNote[] notes)
    {
        var slideCount = 0;
        foreach (var note in notes)
        {
            if (note.Type == SimaiNoteType.Slide)
            {
                slideCount++;
            }
        }
        var otherCount = notes.Length - slideCount;
        foreach (var note in notes)
        {
            if (note.Type == SimaiNoteType.Slide)
            {
                note.IsEach = slideCount > 1;
                continue;
            }
            note.IsEach = otherCount > 1;
            if (note.Type != SimaiNoteType.Tap)
            {
                continue;
            }
            var hasSlides = note.RelatedNotes.Length != 0;
            note.IsStar = note.IsForceStar || (hasSlides && !note.IsTapHeadSlide);
            note.IsStarDouble = note.IsStar && hasSlides && CountSameHeadSlides(notes, note.StartPosition) > 1;
        }
    }

    /// <summary>
    /// Counts the slides of a timing point that start from a button and are not mines.
    /// </summary>
    private static int CountSameHeadSlides(SimaiNote[] notes, int startPosition)
    {
        var count = 0;
        foreach (var note in notes)
        {
            if (note.Type == SimaiNoteType.Slide && !note.IsMine && note.StartPosition == startPosition)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Creates the star head of a slide written with its head.
    /// </summary>
    private static SimaiNote CreateHead(in ParsedNote parsed)
    {
        var head = new SimaiNote
        {
            Type = SimaiNoteType.Tap,
            StartPosition = parsed.StartPosition,
            TouchArea = parsed.TouchArea,
            RawContent = StringCache.Get(parsed.RawContent.AsSpan(0, parsed.HeadLength)),
        };
        ApplyFlags(head, parsed.Flags & HeadFlags);

        return head;
    }

    /// <summary>
    /// Creates a slide path note.
    /// </summary>
    private static SimaiNote CreateSlide(in ParsedNote parsed)
    {
        return new SimaiNote
        {
            Type = SimaiNoteType.Slide,
            StartPosition = parsed.StartPosition,
            TouchArea = parsed.TouchArea,
            SlideStartTime = parsed.SlideStartTime,
            SlideTime = parsed.SlideTime,
            RawContent = parsed.RawContent,
            IsBreak = parsed.Has(ParsedNoteFlags.SlideBreak),
            IsMine = parsed.Has(ParsedNoteFlags.MineSlide),
            UsingSV = parsed.Has(ParsedNoteFlags.UsingSV),
        };
    }

    /// <summary>
    /// Creates a tap, hold, touch or touch hold.
    /// </summary>
    private static SimaiNote CreateNote(in ParsedNote parsed)
    {
        var note = new SimaiNote
        {
            Type = parsed.Type,
            StartPosition = parsed.StartPosition,
            TouchArea = parsed.TouchArea,
            HoldTime = parsed.HoldTime,
            RawContent = parsed.RawContent,
        };
        ApplyFlags(note, parsed.Flags);

        return note;
    }

    /// <summary>
    /// Copies the markers that apply to a note that is not a slide path.
    /// </summary>
    /// <param name="note">The note to change.</param>
    /// <param name="flags">The markers.</param>
    private static void ApplyFlags(SimaiNote note, ParsedNoteFlags flags)
    {
        note.IsBreak = (flags & ParsedNoteFlags.Break) != 0;
        note.IsEx = (flags & ParsedNoteFlags.Ex) != 0;
        note.IsMine = (flags & ParsedNoteFlags.Mine) != 0;
        note.IsForceStar = (flags & ParsedNoteFlags.ForceStar) != 0;
        note.IsFakeRotate = (flags & ParsedNoteFlags.FakeRotate) != 0;
        note.IsTapHeadSlide = (flags & ParsedNoteFlags.TapHeadSlide) != 0;
        note.IsHanabi = (flags & ParsedNoteFlags.Hanabi) != 0;
        note.UsingSV = (flags & ParsedNoteFlags.UsingSV) != 0;
    }
}
