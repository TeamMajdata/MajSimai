#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiFile"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiFile
{
    /// <summary>The length of <see cref="title"/> in bytes.</summary>
    public int titleLen;

    /// <summary>The length of <see cref="artist"/> in bytes.</summary>
    public int artistLen;

    /// <summary>The length of <see cref="finalDesigner"/> in bytes.</summary>
    public int finalDesignerLen;

    /// <summary>The <c>&amp;first</c> offset in seconds.</summary>
    public float offset;

    /// <summary>The number of items in <see cref="charts"/>.</summary>
    public int chartsLen;

    /// <summary>The number of items in <see cref="commands"/>.</summary>
    public int commandsLen;

    /// <summary>The song title.</summary>
    [NativeString(nameof(titleLen))]
    public byte* title;

    /// <summary>The song artist.</summary>
    [NativeString(nameof(artistLen))]
    public byte* artist;

    /// <summary>The designer of the file.</summary>
    [NativeString(nameof(finalDesignerLen))]
    public byte* finalDesigner;

    /// <summary>The seven charts, <c>&amp;inote_1</c> to <c>&amp;inote_7</c>.</summary>
    [NativeArray(nameof(chartsLen))]
    public UnmanagedSimaiChart* charts;

    /// <summary>The other fields of the file.</summary>
    [NativeArray(nameof(commandsLen))]
    public UnmanagedSimaiCommand* commands;

    /// <summary>
    /// Frees the memory owned by the file, its charts and its commands.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)title);
        Marshal.FreeHGlobal((nint)artist);
        Marshal.FreeHGlobal((nint)finalDesigner);
        if (charts is not null)
        {
            for (var i = 0; i < chartsLen; i++)
            {
                (charts + i)->Free();
            }
            Marshal.FreeHGlobal((nint)charts);
            charts = null;
        }
        if (commands is not null)
        {
            for (var i = 0; i < commandsLen; i++)
            {
                (commands + i)->Free();
            }
            Marshal.FreeHGlobal((nint)commands);
            commands = null;
        }
        title = null;
        artist = null;
        finalDesigner = null;
        titleLen = 0;
        artistLen = 0;
        finalDesignerLen = 0;
        chartsLen = 0;
        commandsLen = 0;
    }
}
#endif
