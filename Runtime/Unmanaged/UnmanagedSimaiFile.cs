#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiFile"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiFile
{
    /// <summary>The length of <see cref="title"/>.</summary>
    public int titleLen;

    /// <summary>The length of <see cref="artist"/>.</summary>
    public int artistLen;

    /// <summary>The length of <see cref="finalDesigner"/>.</summary>
    public int finalDesignerLen;

    /// <summary>The <c>&amp;first</c> offset in seconds.</summary>
    public float offset;

    /// <summary>The number of items in <see cref="charts"/>.</summary>
    public int chartsLen;

    /// <summary>The number of items in <see cref="commands"/>.</summary>
    public int commandsLen;

    /// <summary>The song title as ANSI characters.</summary>
    public char* title;

    /// <summary>The song artist as ANSI characters.</summary>
    public char* artist;

    /// <summary>The designer of the file as ANSI characters.</summary>
    public char* finalDesigner;

    /// <summary>The charts.</summary>
    public UnmanagedSimaiChart* charts;

    /// <summary>The other fields.</summary>
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
