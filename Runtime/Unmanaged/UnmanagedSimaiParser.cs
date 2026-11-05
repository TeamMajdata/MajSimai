#if NET7_0_OR_GREATER
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MajSimai.Logging;

namespace MajSimai.Unmanaged;

/// <summary>
/// The native entry points of MajSimai, exported when the library is published with native AOT.
/// </summary>
/// <remarks>
/// The C++ wrapper in <c>Bindings/Cpp</c> is generated from these entry points and the structs they use by
/// <c>tools/MajSimai.CppGen</c>. Run the generator again after changing the native ABI.
/// </remarks>
public static unsafe class UnmanagedSimaiParser
{
    /// <summary>
    /// Parses a simai file in lenient mode. The text is read as UTF-8 unless it starts with a byte order mark. Free the
    /// result with <c>MajSimai_Free</c>, whether parsing succeeded or not.
    /// </summary>
    /// <param name="content">The simai file.</param>
    /// <param name="contentLength">The length of the file in bytes.</param>
    /// <param name="result">Receives the parsed file, or the error that stopped the parse.</param>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Parse", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Parse([NativeString(nameof(contentLength))] byte* content, int contentLength, UnmanagedSimaiParseResult* result)
    {
        if (result is null)
        {
            return;
        }
        if (content is null || contentLength <= 0)
        {
            *result = CreateError(UnmanagedSimaiParseResult.CODE_INVALID_CONTENT, "The content is empty.");
            return;
        }
        try
        {
            using var stream = new UnmanagedMemoryStream(content, contentLength);
            var simaiFile = SimaiParser.Parse(stream);
            var unmanagedFile = (UnmanagedSimaiFile*)Marshal.AllocHGlobal(sizeof(UnmanagedSimaiFile));
            *unmanagedFile = UnmanagedConverter.ToUnmanaged(simaiFile);
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_NO_ERROR,
                simaiFile = unmanagedFile,
            };
        }
        catch (Exception e)
        {
            *result = CreateError(e);
        }
    }

    /// <summary>
    /// Frees a result returned by <c>MajSimai_Parse</c> and resets it.
    /// </summary>
    /// <param name="ptr">The result.</param>
    /// <returns><see langword="true"/> when the result was freed; <see langword="false"/> when it is null.</returns>
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
        catch (Exception e)
        {
            SimaiLogger.Error("MajSimai_Free failed.", e);
            return false;
        }
    }

    /// <summary>
    /// Frees memory allocated by MajSimai with <see cref="Marshal.AllocHGlobal(int)"/>.
    /// </summary>
    /// <param name="ptr">The memory.</param>
    /// <returns><see langword="true"/> when the memory was freed; <see langword="false"/> when it is null.</returns>
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
        catch (Exception e)
        {
            SimaiLogger.Error("MajSimai_FreeHGlobal failed.", e);
            return false;
        }
    }

    /// <summary>
    /// Creates the failed result for an exception thrown while parsing, and logs the exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>
    /// A result with <see cref="UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX"/> or
    /// <see cref="UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP"/> and the position of the problem for simai
    /// errors, or <see cref="UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR"/> and the full exception text otherwise.
    /// </returns>
    internal static UnmanagedSimaiParseResult CreateError(Exception exception)
    {
        if (exception is not InvalidSimaiMarkupException markup)
        {
            SimaiLogger.Error("MajSimai_Parse failed.", exception);
            return CreateError(UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR, exception.ToString());
        }
        SimaiLogger.Warning($"MajSimai_Parse rejected the content at line {markup.Line}, column {markup.Column}: {markup.Message}");
        var code = markup is InvalidSimaiSyntaxException
            ? UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX
            : UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP;
        var result = CreateError(code, markup.Message);
        result.errorAtLine = markup.Line;
        result.errorAtColumn = markup.Column;
        result.errorContent = UnmanagedConverter.ToUtf8(markup.Content, out result.errorContentLen);

        return result;
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="code">One of the <c>CODE_</c> constants of <see cref="UnmanagedSimaiParseResult"/>.</param>
    /// <param name="message">The description of the error.</param>
    /// <returns>The result, which owns a native copy of <paramref name="message"/>.</returns>
    private static UnmanagedSimaiParseResult CreateError(int code, string message)
    {
        var result = new UnmanagedSimaiParseResult { code = code };
        result.errorMsg = UnmanagedConverter.ToUtf8(message, out result.errorMsgLen);

        return result;
    }
}
#endif
