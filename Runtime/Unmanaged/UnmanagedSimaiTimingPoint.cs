#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiTimingPoint"/>.
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

    /// <summary>
    /// Frees the memory owned by the timing point and its notes.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)rawContent);
        if (notes is not null)
        {
            for (var i = 0; i < notesLen; i++)
            {
                (notes + i)->Free();
            }
            Marshal.FreeHGlobal((nint)notes);
            notes = null;
        }
        rawContent = null;
        rawContentLen = 0;
        notesLen = 0;
    }
}
#endif
