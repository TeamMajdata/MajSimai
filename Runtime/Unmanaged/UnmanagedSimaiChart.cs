#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiChart"/>. Its pointers belong to the parse result that holds it and are freed
/// by <c>MajSimai_Free</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiChart
{
    /// <summary>The length of <see cref="level"/> in bytes.</summary>
    public int levelLen;

    /// <summary>The length of <see cref="designer"/> in bytes.</summary>
    public int designerLen;

    /// <summary>The length of <see cref="fumen"/> in bytes.</summary>
    public int fumenLen;

    /// <summary>The number of items in <see cref="noteTimings"/>.</summary>
    public int noteTimingsLen;

    /// <summary>The number of items in <see cref="commaTimings"/>.</summary>
    public int commaTimingsLen;

    /// <summary>Whether the chart has no note timing points.</summary>
    public bool isEmpty;

    /// <summary>The level text.</summary>
    [NativeString(nameof(levelLen))]
    public byte* level;

    /// <summary>The designer of the chart.</summary>
    [NativeString(nameof(designerLen))]
    public byte* designer;

    /// <summary>The chart text.</summary>
    [NativeString(nameof(fumenLen))]
    public byte* fumen;

    /// <summary>The timing points that have notes.</summary>
    [NativeArray(nameof(noteTimingsLen))]
    public UnmanagedSimaiTimingPoint* noteTimings;

    /// <summary>The timing points of the commas.</summary>
    [NativeArray(nameof(commaTimingsLen))]
    public UnmanagedSimaiTimingPoint* commaTimings;
}
#endif
