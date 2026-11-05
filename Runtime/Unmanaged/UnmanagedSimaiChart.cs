#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiChart"/>.
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

    /// <summary>
    /// Frees the memory owned by the chart and its timing points.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)level);
        Marshal.FreeHGlobal((nint)designer);
        Marshal.FreeHGlobal((nint)fumen);
        FreeTimings(noteTimings, noteTimingsLen);
        FreeTimings(commaTimings, commaTimingsLen);
        level = null;
        levelLen = 0;
        designer = null;
        designerLen = 0;
        fumen = null;
        fumenLen = 0;
        isEmpty = true;
        noteTimings = null;
        noteTimingsLen = 0;
        commaTimings = null;
        commaTimingsLen = 0;
    }

    /// <summary>
    /// Frees native timing points and the array that holds them.
    /// </summary>
    /// <param name="timings">The array, or null.</param>
    /// <param name="length">The number of timing points.</param>
    private static void FreeTimings(UnmanagedSimaiTimingPoint* timings, int length)
    {
        if (timings is null)
        {
            return;
        }
        for (var i = 0; i < length; i++)
        {
            (timings + i)->Free();
        }
        Marshal.FreeHGlobal((nint)timings);
    }
}
#endif
