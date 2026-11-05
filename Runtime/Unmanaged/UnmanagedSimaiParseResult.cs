#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The result of <c>MajSimai_Parse</c> and <c>MajSimai_ParseWithTolerance</c>: a parsed file, or the error that stopped
/// the parse. The caller owns the result and frees it with <c>MajSimai_Free</c>; all the memory it points to belongs to
/// it, so never free those pointers yourself, never keep them after <c>MajSimai_Free</c>, and never free two copies of
/// one result.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe ref struct UnmanagedSimaiParseResult
{
    /// <summary>The file was parsed.</summary>
    public const int CODE_NO_ERROR = 0;

    /// <summary>The content was empty.</summary>
    public const int CODE_INVALID_CONTENT = 1;

    /// <summary>The parser failed for a reason other than the content.</summary>
    public const int CODE_INTERNAL_ERROR = 2;

    /// <summary>The content has malformed markup, such as a malformed note.</summary>
    public const int CODE_INVALID_SIMAI_MARKUP = 3;

    /// <summary>The content has a malformed declaration, such as a BPM change that is not closed.</summary>
    public const int CODE_INVALID_SIMAI_SYNTAX = 4;

    /// <summary>An argument other than the content is not valid, such as an unknown syntax tolerance.</summary>
    public const int CODE_INVALID_ARGUMENT = 5;

    /// <summary>One of the <c>CODE_</c> constants.</summary>
    public required int code;

    /// <summary>The line of the error, starting from 1, or -1.</summary>
    public int errorAtLine = -1;

    /// <summary>The column of the error, or -1.</summary>
    public int errorAtColumn = -1;

    /// <summary>The length of <see cref="errorMsg"/> in bytes.</summary>
    public int errorMsgLen = 0;

    /// <summary>The length of <see cref="errorContent"/> in bytes.</summary>
    public int errorContentLen = 0;

    /// <summary>The parsed file, or null when parsing failed.</summary>
    [NativePointee(typeof(UnmanagedSimaiFile))]
    public void* simaiFile = null;

    /// <summary>The description of the error, or null when parsing succeeded.</summary>
    [NativeString(nameof(errorMsgLen))]
    public byte* errorMsg = null;

    /// <summary>The text the error is about, or null.</summary>
    [NativeString(nameof(errorContentLen))]
    public byte* errorContent = null;

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public UnmanagedSimaiParseResult()
    {
    }

    /// <summary>
    /// Frees the memory owned by the result. The parsed file and everything it points to are one block of native
    /// memory, so it is freed with one call.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)errorMsg);
        Marshal.FreeHGlobal((nint)errorContent);
        Marshal.FreeHGlobal((nint)simaiFile);
        simaiFile = null;
        errorMsg = null;
        errorContent = null;
        code = -1;
        errorAtLine = -1;
        errorAtColumn = -1;
        errorMsgLen = 0;
        errorContentLen = 0;
    }
}
#endif
