#if NET7_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MajSimai.Logging;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native entry points of MajSimai, exported when the library is published with native AOT.
/// </summary>
public static unsafe class UnmanagedSimaiParser
{
    /// <summary>
    /// Parses ANSI simai text. Free the result with <c>MajSimai_Free</c>.
    /// </summary>
    /// <param name="contentPtr">The simai text as ANSI characters.</param>
    /// <param name="contentLen">The length of the text.</param>
    /// <param name="result">Receives the result.</param>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Parse", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Parse(char* contentPtr, int contentLen, UnmanagedSimaiParseResult* result)
    {
        var content = Marshal.PtrToStringAnsi((nint)contentPtr, contentLen);
        if (string.IsNullOrEmpty(content))
        {
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_INVALID_CONTENT
            };
            return;
        }
        try
        {
            var simaiFile = SimaiParser.Parse(content.AsSpan(), string.Empty);
            var unmanagedFilePtr = (UnmanagedSimaiFile*)Marshal.AllocHGlobal(sizeof(UnmanagedSimaiFile));
            *unmanagedFilePtr = UnmanagedConverter.ToUnmanaged(simaiFile);
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_NO_ERROR,
                simaiFile = unmanagedFilePtr
            };
        }
        catch (Exception e)
        {
            SimaiLogger.Error("MajSimai_Parse failed.", e);
            var errorMsg = e.ToString();
            var line = -1;
            var column = -1;
            var errorContent = (string?)null;
            if (e is InvalidSimaiMarkupException markupException)
            {
                line = markupException.Line;
                column = markupException.Column;
                errorContent = markupException.Content;
            }
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR,
                errorMsgAnsi = (char*)Marshal.StringToHGlobalAnsi(errorMsg),
                errorMsgLen = errorMsg.Length,
                errorAtLine = line,
                errorAtColumn = column,
                errorContentAnsi = string.IsNullOrEmpty(errorContent) ? null : (char*)Marshal.StringToHGlobalAnsi(errorContent),
                errorContentLen = errorContent?.Length ?? 0,
            };
        }
    }

    /// <summary>
    /// Frees a result returned by <c>MajSimai_Parse</c>.
    /// </summary>
    /// <param name="ptr">The result.</param>
    /// <returns><see langword="true"/> when the result was freed.</returns>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Free", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static bool Free(UnmanagedSimaiParseResult* ptr)
    {
        try
        {
            if (ptr is null)
            {
                return false;
            }
            ptr->Free();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Frees memory allocated by MajSimai with <see cref="Marshal.AllocHGlobal(int)"/>.
    /// </summary>
    /// <param name="ptr">The memory.</param>
    /// <returns><see langword="true"/> when the memory was freed.</returns>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_FreeHGlobal", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static bool Free(nint ptr)
    {
        try
        {
            if (ptr == 0)
            {
                return false;
            }
            Marshal.FreeHGlobal(ptr);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif
