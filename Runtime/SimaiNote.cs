using System;
using System.Runtime.CompilerServices;

namespace MajSimai;

/// <summary>
/// A note of a simai chart.
/// </summary>
/// <remarks>
/// A slide with a star head is read as two kinds of notes: a <see cref="SimaiNoteType.Tap"/> head with
/// <see cref="IsStar"/> set, followed by one <see cref="SimaiNoteType.Slide"/> note for each slide path. The head lists
/// its slides in <see cref="RelatedNotes"/>. A slide without a head (<c>1?-5[8:1]</c> or <c>1!-5[8:1]</c>) is only a
/// <see cref="SimaiNoteType.Slide"/> note.
/// </remarks>
public sealed class SimaiNote
{
    /// <summary>
    /// The boolean properties of the note.
    /// </summary>
    private NoteFlags _flags;

    /// <summary>
    /// The notes this note belongs with.
    /// </summary>
    private SimaiNote[] _relatedNotes = Array.Empty<SimaiNote>();

    /// <summary>
    /// The note text without markers.
    /// </summary>
    private string _rawContent = string.Empty;

    /// <summary>
    /// The boolean properties of a note, packed into one field.
    /// </summary>
    [Flags]
    private enum NoteFlags : ushort
    {
        /// <summary>No flags.</summary>
        None = 0,
        /// <summary><see cref="IsBreak"/>.</summary>
        Break = 1 << 0,
        /// <summary><see cref="IsEx"/>.</summary>
        Ex = 1 << 1,
        /// <summary><see cref="IsFakeRotate"/>.</summary>
        FakeRotate = 1 << 2,
        /// <summary><see cref="IsForceStar"/>.</summary>
        ForceStar = 1 << 3,
        /// <summary><see cref="IsHanabi"/>.</summary>
        Hanabi = 1 << 4,
        /// <summary><see cref="IsTapHeadSlide"/>.</summary>
        TapHeadSlide = 1 << 5,
        /// <summary><see cref="IsMine"/>.</summary>
        Mine = 1 << 6,
        /// <summary><see cref="UsingSV"/>.</summary>
        UsingSV = 1 << 7,
        /// <summary><see cref="IsEach"/>.</summary>
        Each = 1 << 8,
        /// <summary><see cref="IsStar"/>.</summary>
        Star = 1 << 9,
        /// <summary><see cref="IsStarDouble"/>.</summary>
        StarDouble = 1 << 10
    }

    /// <summary>
    /// Gets or sets the kind of the note.
    /// </summary>
    public SimaiNoteType Type { get; set; }

    /// <summary>
    /// Gets or sets the button (1 to 8) of a button note, or the sensor index of a touch note. A <c>C</c> touch note uses
    /// 8. A slide uses the button its path starts from.
    /// </summary>
    public int StartPosition { get; set; } = 1;

    /// <summary>
    /// Gets or sets the touch area letter (<c>A</c> to <c>E</c>) of a touch note, or a space for other notes.
    /// </summary>
    public char TouchArea { get; set; } = ' ';

    /// <summary>
    /// Gets or sets the length in seconds of a <see cref="SimaiNoteType.Hold"/> or <see cref="SimaiNoteType.TouchHold"/>.
    /// </summary>
    public double HoldTime { get; set; }

    /// <summary>
    /// Gets or sets the time in seconds, from the start of the chart, at which a <see cref="SimaiNoteType.Slide"/> starts
    /// moving.
    /// </summary>
    public double SlideStartTime { get; set; }

    /// <summary>
    /// Gets or sets how long in seconds a <see cref="SimaiNoteType.Slide"/> moves.
    /// </summary>
    public double SlideTime { get; set; }

    /// <summary>
    /// Gets or sets the note text without the markers that became flags. A slide keeps its whole path (<c>1-5[8:1]</c>);
    /// the star head of a slide only keeps the text before the first slide mark (<c>1</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    public string RawContent
    {
        get => _rawContent;
        set => _rawContent = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the notes this note belongs with. The star head of a slide lists its slides here, in text order;
    /// other notes have an empty array. Slides do not point back to their head.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    public SimaiNote[] RelatedNotes
    {
        get => _relatedNotes;
        set => _relatedNotes = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets whether the note is a break note (<c>b</c>). On a slide it marks a break slide path.
    /// </summary>
    public bool IsBreak
    {
        get => HasFlag(NoteFlags.Break);
        set => SetFlag(NoteFlags.Break, value);
    }

    /// <summary>
    /// Gets or sets whether the note is an EX note (<c>x</c>).
    /// </summary>
    public bool IsEx
    {
        get => HasFlag(NoteFlags.Ex);
        set => SetFlag(NoteFlags.Ex, value);
    }

    /// <summary>
    /// Gets or sets whether the star uses the fake rotation marker <c>$$</c>.
    /// </summary>
    public bool IsFakeRotate
    {
        get => HasFlag(NoteFlags.FakeRotate);
        set => SetFlag(NoteFlags.FakeRotate, value);
    }

    /// <summary>
    /// Gets or sets whether the note uses the force star marker <c>$</c>.
    /// </summary>
    public bool IsForceStar
    {
        get => HasFlag(NoteFlags.ForceStar);
        set => SetFlag(NoteFlags.ForceStar, value);
    }

    /// <summary>
    /// Gets or sets whether the touch note uses the hanabi marker <c>f</c>.
    /// </summary>
    public bool IsHanabi
    {
        get => HasFlag(NoteFlags.Hanabi);
        set => SetFlag(NoteFlags.Hanabi, value);
    }

    /// <summary>
    /// Gets or sets whether the head of a slide is drawn as a tap instead of a star (<c>@</c>).
    /// </summary>
    public bool IsTapHeadSlide
    {
        get => HasFlag(NoteFlags.TapHeadSlide);
        set => SetFlag(NoteFlags.TapHeadSlide, value);
    }

    /// <summary>
    /// Gets or sets whether the note is a mine (<c>m</c>). On a slide it marks a mine slide path.
    /// </summary>
    public bool IsMine
    {
        get => HasFlag(NoteFlags.Mine);
        set => SetFlag(NoteFlags.Mine, value);
    }

    /// <summary>
    /// Gets or sets whether the note follows the slide velocity (<c>SV</c>); the <c>c</c> marker turns it off.
    /// </summary>
    public bool UsingSV
    {
        get => HasFlag(NoteFlags.UsingSV);
        set => SetFlag(NoteFlags.UsingSV, value);
    }

    /// <summary>
    /// Gets or sets whether the note is part of an each (a group of notes hit at the same time). A slide is an each
    /// when its timing point has more than one slide; any other note is an each when its timing point has more than one
    /// note that is not a slide.
    /// </summary>
    public bool IsEach
    {
        get => HasFlag(NoteFlags.Each);
        set => SetFlag(NoteFlags.Each, value);
    }

    /// <summary>
    /// Gets or sets whether a <see cref="SimaiNoteType.Tap"/> is drawn as a star: it uses <c>$</c>, or it is the head of
    /// slides and does not use <c>@</c>.
    /// </summary>
    public bool IsStar
    {
        get => HasFlag(NoteFlags.Star);
        set => SetFlag(NoteFlags.Star, value);
    }

    /// <summary>
    /// Gets or sets whether a star is drawn as a double star: it is the head of slides, and its timing point has more
    /// than one slide that starts from the same button and is not a mine.
    /// </summary>
    public bool IsStarDouble
    {
        get => HasFlag(NoteFlags.StarDouble);
        set => SetFlag(NoteFlags.StarDouble, value);
    }

    /// <summary>
    /// Gets whether a flag is set.
    /// </summary>
    /// <param name="flag">The flag.</param>
    /// <returns><see langword="true"/> when the flag is set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasFlag(NoteFlags flag)
    {
        return (_flags & flag) != 0;
    }

    /// <summary>
    /// Sets or clears a flag.
    /// </summary>
    /// <param name="flag">The flag.</param>
    /// <param name="value">Whether the flag is set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlag(NoteFlags flag, bool value)
    {
        if (value)
        {
            _flags |= flag;
        }
        else
        {
            _flags &= ~flag;
        }
    }
}
