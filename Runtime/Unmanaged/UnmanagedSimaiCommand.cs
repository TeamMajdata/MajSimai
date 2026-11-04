#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiCommand"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiCommand
{
    /// <summary>The length of <see cref="prefix"/>.</summary>
    public int prefixLen;

    /// <summary>The length of <see cref="value"/>.</summary>
    public int valueLen;

    /// <summary>The field name as ANSI characters.</summary>
    public char* prefix;

    /// <summary>The field value as ANSI characters.</summary>
    public char* value;

    /// <summary>
    /// Frees the memory owned by the command.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)prefix);
        Marshal.FreeHGlobal((nint)value);
        prefix = null;
        value = null;
        prefixLen = 0;
        valueLen = 0;
    }
}
#endif
