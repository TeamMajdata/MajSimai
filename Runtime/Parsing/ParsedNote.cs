using System;

namespace MajSimai.Parsing;

/// <summary>
/// The markers found in the text of one note, as MajSimai 2.x read them.
/// </summary>
[Flags]
internal enum ParsedNoteFlags : ushort
{
    /// <summary>No markers.</summary>
    None = 0,
    /// <summary><c>b</c> before the slide path, or on a note that is not a slide.</summary>
    Break = 1 << 0,
    /// <summary><c>b</c> on the slide path.</summary>
    SlideBreak = 1 << 1,
    /// <summary><c>x</c>.</summary>
    Ex = 1 << 2,
    /// <summary><c>$</c>.</summary>
    ForceStar = 1 << 3,
    /// <summary><c>$$</c>.</summary>
    FakeRotate = 1 << 4,
    /// <summary><c>@</c>.</summary>
    TapHeadSlide = 1 << 5,
    /// <summary><c>m</c> before the slide path, or on a note that is not a slide.</summary>
    Mine = 1 << 6,
    /// <summary><c>m</c> on the slide path.</summary>
    MineSlide = 1 << 7,
    /// <summary>No <c>c</c> marker.</summary>
    UsingSV = 1 << 8,
    /// <summary><c>f</c>.</summary>
    Hanabi = 1 << 9,
    /// <summary>A slide without a head: <c>!</c>, <c>?</c>, or a later part of <c>*</c>.</summary>
    NoHead = 1 << 10
}

/// <summary>
/// How a note relates to the <c>*</c> (same head) syntax.
/// </summary>
internal enum SameHeadRole : byte
{
    /// <summary>The note is not part of a <c>*</c> group.</summary>
    None,
    /// <summary>The note is the first part of a <c>*</c> group.</summary>
    First,
    /// <summary>The note is a later part of a <c>*</c> group, read with the first character of the first part.</summary>
    Continuation
}

/// <summary>
/// A note read from text, in the MajSimai 2.x shape where a slide carries its own star head. The note assembler turns
/// it into <see cref="SimaiNote"/> objects.
/// </summary>
internal struct ParsedNote
{
    /// <summary>The kind of the note.</summary>
    public SimaiNoteType Type;

    /// <summary>The button or sensor index.</summary>
    public int StartPosition;

    /// <summary>The touch area letter, or a space.</summary>
    public char TouchArea;

    /// <summary>The hold length in seconds.</summary>
    public double HoldTime;

    /// <summary>The time in seconds at which the slide starts moving.</summary>
    public double SlideStartTime;

    /// <summary>The slide length in seconds.</summary>
    public double SlideTime;

    /// <summary>The markers of the note.</summary>
    public ParsedNoteFlags Flags;

    /// <summary>The note text without markers.</summary>
    public string RawContent;

    /// <summary>For a slide, the length of the text before the first slide mark (the star head).</summary>
    public int HeadLength;

    /// <summary>How the note relates to the <c>*</c> syntax.</summary>
    public SameHeadRole Role;

    /// <summary>
    /// Gets whether all the given flags are set.
    /// </summary>
    /// <param name="flags">The flags to check.</param>
    /// <returns><see langword="true"/> when every flag in <paramref name="flags"/> is set.</returns>
    public readonly bool Has(ParsedNoteFlags flags)
    {
        return (Flags & flags) == flags;
    }
}
