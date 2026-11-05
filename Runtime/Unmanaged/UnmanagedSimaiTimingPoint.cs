#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiTimingPoint"/>. Its pointers belong to the parse result that holds it and are
/// freed by <c>MajSimai_Free</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiTimingPoint
{
    /// <summary>The time in seconds from the start of the chart.</summary>
    public double timing;

    /// <summary>The BPM in effect.</summary>
    public float bpm;

    /// <summary>The note speed multiplier in effect.</summary>
    public float hSpeed;

    /// <summary>The slide velocity multiplier in effect.</summary>
    public float sVeloc;

    /// <summary>The column in the chart text.</summary>
    public int rawTextPositionX;

    /// <summary>The line in the chart text.</summary>
    public int rawTextPositionY;

    /// <summary>The character offset in the chart text.</summary>
    public int rawTextPosition;

    /// <summary>The number of items in <see cref="notes"/>.</summary>
    public int notesLen;

    /// <summary>The length of <see cref="rawContent"/> in bytes.</summary>
    public int rawContentLen;

    /// <summary>Whether the timing point has no notes.</summary>
    public bool isEmpty;

    /// <summary>The notes.</summary>
    [NativeArray(nameof(notesLen))]
    public UnmanagedSimaiNote* notes;

    /// <summary>The note text without whitespace.</summary>
    [NativeString(nameof(rawContentLen))]
    public byte* rawContent;
}
#endif
