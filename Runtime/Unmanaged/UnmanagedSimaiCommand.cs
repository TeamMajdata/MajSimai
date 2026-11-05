#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native layout of a <see cref="SimaiCommand"/>. Its pointers belong to the parse result that holds it and are
/// freed by <c>MajSimai_Free</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiCommand
{
    /// <summary>The length of <see cref="prefix"/> in bytes.</summary>
    public int prefixLen;

    /// <summary>The length of <see cref="value"/> in bytes.</summary>
    public int valueLen;

    /// <summary>The field name, without the <c>&amp;</c>.</summary>
    [NativeString(nameof(prefixLen))]
    public byte* prefix;

    /// <summary>The field value.</summary>
    [NativeString(nameof(valueLen))]
    public byte* value;
}
#endif
