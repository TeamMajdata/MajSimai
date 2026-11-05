#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiNote"/>. Its pointers belong to the parse result that holds it and are freed
/// by <c>MajSimai_Free</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiNote
{
    /// <summary>The kind of the note.</summary>
    public SimaiNoteType type;

    /// <summary>The button or sensor index.</summary>
    public int startPosition;

    /// <summary>The hold length in seconds.</summary>
    public double holdTime;

    /// <summary>The time in seconds at which a slide starts moving.</summary>
    public double slideStartTime;

    /// <summary>The slide length in seconds.</summary>
    public double slideTime;

    /// <summary>The touch area letter, or a space.</summary>
    public char touchArea;

    /// <summary>Whether the note (or slide path) is a break.</summary>
    public bool isBreak;

    /// <summary>Whether the note is an EX note.</summary>
    public bool isEx;

    /// <summary>Whether the star uses fake rotation.</summary>
    public bool isFakeRotate;

    /// <summary>Whether the note uses the force star marker.</summary>
    public bool isForceStar;

    /// <summary>Whether the touch note is a hanabi.</summary>
    public bool isHanabi;

    /// <summary>Whether the slide head is drawn as a tap.</summary>
    public bool isTapHeadSlide;

    /// <summary>Whether the note (or slide path) is a mine.</summary>
    public bool isMine;

    /// <summary>Whether the note follows the slide velocity.</summary>
    public bool usingSV;

    /// <summary>Whether the note is part of an each.</summary>
    public bool isEach;

    /// <summary>Whether the tap is drawn as a star.</summary>
    public bool isStar;

    /// <summary>Whether the star is drawn as a double star.</summary>
    public bool isStarDouble;

    /// <summary>The length of <see cref="rawContent"/> in bytes.</summary>
    public int rawContentLen;

    /// <summary>The number of items in <see cref="relatedNotes"/>.</summary>
    public int relatedNotesLen;

    /// <summary>The note text.</summary>
    [NativeString(nameof(rawContentLen))]
    public byte* rawContent;

    /// <summary>
    /// The indices, in the notes of the same timing point, of <see cref="SimaiNote.RelatedNotes"/>; -1 for a related
    /// note that is not in the timing point.
    /// </summary>
    [NativeArray(nameof(relatedNotesLen))]
    public int* relatedNotes;

    /// <summary>
    /// Frees the memory owned by the note.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)rawContent);
        Marshal.FreeHGlobal((nint)relatedNotes);
        rawContent = null;
        rawContentLen = 0;
        relatedNotes = null;
        relatedNotesLen = 0;
    }
}
#endif
