#if NET7_0_OR_GREATER
using System.Runtime.InteropServices;

namespace MajSimai.Unmanaged;

/// <summary>
/// The result of <c>MajSimai_Parse</c>: a parsed file, or the error that stopped the parse.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe ref struct UnmanagedSimaiParseResult
{
    /// <summary>The file was parsed.</summary>
    public const int CODE_NO_ERROR = 0;

    /// <summary>The content was empty.</summary>
    public const int CODE_INVALID_CONTENT = 1;

    /// <summary>The parser failed.</summary>
    public const int CODE_INTERNAL_ERROR = 2;

    /// <summary>The content has malformed markup.</summary>
    public const int CODE_INVALID_SIMAI_MARKUP = 3;

    /// <summary>The content has a malformed declaration.</summary>
    public const int CODE_INVALID_SIMAI_SYNTAX = 4;

    /// <summary>One of the <c>CODE_</c> constants.</summary>
    public required int code;

    /// <summary>The line of the error, or -1.</summary>
    public int errorAtLine = -1;

    /// <summary>The column of the error, or -1.</summary>
    public int errorAtColumn = -1;

    /// <summary>The length of <see cref="errorMsgAnsi"/>.</summary>
    public int errorMsgLen = 0;

    /// <summary>The length of <see cref="errorContentAnsi"/>.</summary>
    public int errorContentLen = 0;

    /// <summary>The parsed file (an <c>UnmanagedSimaiFile</c>), or null when parsing failed.</summary>
    public void* simaiFile = null;

    /// <summary>The error message as ANSI characters, or null.</summary>
    public char* errorMsgAnsi = null;

    /// <summary>The text the error is about as ANSI characters, or null.</summary>
    public char* errorContentAnsi = null;

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public UnmanagedSimaiParseResult()
    {
    }

    /// <summary>
    /// Frees the memory owned by the result.
    /// </summary>
    public void Free()
    {
        Marshal.FreeHGlobal((nint)errorMsgAnsi);
        Marshal.FreeHGlobal((nint)errorContentAnsi);
        if (simaiFile is not null)
        {
            ((UnmanagedSimaiFile*)simaiFile)->Free();
            Marshal.FreeHGlobal((nint)simaiFile);
            simaiFile = null;
        }
        errorMsgAnsi = null;
        errorContentAnsi = null;
        code = -1;
        errorAtLine = -1;
        errorAtColumn = -1;
        errorMsgLen = 0;
        errorContentLen = 0;
    }
}
#endif
