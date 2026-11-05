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
/// <para>
/// The caller owns every result: free it with <c>MajSimai_Free</c> exactly once, whether parsing succeeded or not. All
/// the memory a result points to belongs to the result; never free a pointer taken from it, and never keep one after
/// <c>MajSimai_Free</c>.
/// </para>
/// <para>
/// The signatures only use blittable types (no <see cref="bool"/> or <see cref="char"/>), so the entry points work
/// with runtime marshalling enabled. The C++ wrapper in <c>Bindings/Cpp</c> is generated from these entry points and
/// the structs they use by <c>tools/MajSimai.CppGen</c>; run the generator again after changing the native ABI.
/// </para>
/// </remarks>
public static unsafe class UnmanagedSimaiParser
{
    /// <summary>
    /// Parses a simai file in lenient mode. The text is read as UTF-8 unless it starts with a byte order mark.
    /// </summary>
    /// <param name="content">The simai file.</param>
    /// <param name="contentLength">The length of the file in bytes.</param>
    /// <param name="result">
    /// Receives the parsed file, or the error that stopped the parse. It is overwritten without being freed, so it must
    /// not own memory: pass a zero-initialized result or one already freed with <c>MajSimai_Free</c>.
    /// </param>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Parse", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Parse([NativeString(nameof(contentLength))] byte* content, int contentLength, UnmanagedSimaiParseResult* result)
    {
        Parse(content, contentLength, SimaiParserOptions.Lenient, result);
    }

    /// <summary>
    /// Parses a simai file with the given syntax tolerance. The text is read as UTF-8 unless it starts with a byte order
    /// mark. In strict mode the first chart that breaks the grammar fails the parse with
    /// <see cref="UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX"/> or
    /// <see cref="UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP"/>; the line and column of the error are
    /// counted in the text of that chart.
    /// </summary>
    /// <param name="content">The simai file.</param>
    /// <param name="contentLength">The length of the file in bytes.</param>
    /// <param name="tolerance">Lenient or strict; another value fails with <see cref="UnmanagedSimaiParseResult.CODE_INVALID_ARGUMENT"/>.</param>
    /// <param name="result">
    /// Receives the parsed file, or the error that stopped the parse. It is overwritten without being freed, so it must
    /// not own memory: pass a zero-initialized result or one already freed with <c>MajSimai_Free</c>.
    /// </param>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_ParseWithTolerance", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void ParseWithTolerance([NativeString(nameof(contentLength))] byte* content,
                                          int contentLength,
                                          SimaiSyntaxTolerance tolerance,
                                          UnmanagedSimaiParseResult* result)
    {
        if (result is null)
        {
            return;
        }
        if (tolerance is not (SimaiSyntaxTolerance.Lenient or SimaiSyntaxTolerance.Strict))
        {
            *result = CreateError(UnmanagedSimaiParseResult.CODE_INVALID_ARGUMENT, $"The tolerance {(int)tolerance} is neither lenient (0) nor strict (1).");
            return;
        }
        var options = tolerance == SimaiSyntaxTolerance.Strict ? SimaiParserOptions.Strict : SimaiParserOptions.Lenient;
        Parse(content, contentLength, options, result);
    }

    /// <summary>
    /// Frees a result returned by <c>MajSimai_Parse</c> or <c>MajSimai_ParseWithTolerance</c> and resets it to an empty
    /// result. Freeing a result that was already freed does nothing, but freeing two copies of one result frees the
    /// same memory twice.
    /// </summary>
    /// <param name="ptr">The result.</param>
    /// <returns>1 when the result was freed; 0 when <paramref name="ptr"/> is null or freeing failed.</returns>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Free", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Free(UnmanagedSimaiParseResult* ptr)
    {
        try
        {
            if (ptr is null)
            {
                return 0;
            }
            ptr->Free();
            return 1;
        }
        catch (Exception e)
        {
            SimaiLogger.Error("MajSimai_Free failed.", e);
            return 0;
        }
    }

    /// <summary>
    /// Frees one block of memory that MajSimai allocated with <see cref="Marshal.AllocHGlobal(int)"/> and handed over to
    /// the caller. No entry point hands memory over at the moment: everything a parse result points to belongs to the
    /// result and is freed by <c>MajSimai_Free</c>, so never pass a pointer taken from a result.
    /// </summary>
    /// <param name="ptr">The memory.</param>
    /// <returns>1 when the memory was freed; 0 when <paramref name="ptr"/> is null or freeing failed.</returns>
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_FreeHGlobal", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Free(nint ptr)
    {
        try
        {
            if (ptr == 0)
            {
                return 0;
            }
            Marshal.FreeHGlobal(ptr);
            return 1;
        }
        catch (Exception e)
        {
            SimaiLogger.Error("MajSimai_FreeHGlobal failed.", e);
            return 0;
        }
    }

    /// <summary>
    /// Creates the successful result of a parsed file. On failure nothing stays allocated.
    /// </summary>
    /// <param name="file">The parsed file.</param>
    /// <returns>A result that owns a native copy of <paramref name="file"/>.</returns>
    internal static UnmanagedSimaiParseResult CreateSuccess(SimaiFile file)
    {
        var native = UnmanagedConverter.ToUnmanaged(file);
        UnmanagedSimaiFile* pointer;
        try
        {
            pointer = (UnmanagedSimaiFile*)Marshal.AllocHGlobal(sizeof(UnmanagedSimaiFile));
        }
        catch
        {
            native.Free();
            throw;
        }
        *pointer = native;

        return new UnmanagedSimaiParseResult
        {
            code = UnmanagedSimaiParseResult.CODE_NO_ERROR,
            simaiFile = pointer,
        };
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
            SimaiLogger.Error("The native parser failed.", exception);
            return CreateError(UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR, exception.ToString());
        }
        SimaiLogger.Warning($"The native parser rejected the content at line {markup.Line}, column {markup.Column}: {markup.Message}");
        var code = markup is InvalidSimaiSyntaxException
            ? UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX
            : UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP;
        var result = CreateError(code, markup.Message);
        try
        {
            result.errorContent = UnmanagedConverter.ToUtf8(markup.Content, out result.errorContentLen);
        }
        catch
        {
            result.Free();
            throw;
        }
        result.errorAtLine = markup.Line;
        result.errorAtColumn = markup.Column;

        return result;
    }

    /// <summary>
    /// Parses a simai file into a native result.
    /// </summary>
    /// <param name="content">The simai file.</param>
    /// <param name="contentLength">The length of the file in bytes.</param>
    /// <param name="options">The parser options.</param>
    /// <param name="result">Receives the result; nothing happens when it is null.</param>
    private static void Parse(byte* content, int contentLength, SimaiParserOptions options, UnmanagedSimaiParseResult* result)
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
            SimaiFile file;
            using (var stream = new UnmanagedMemoryStream(content, contentLength))
            {
                file = SimaiParser.Parse(stream, options);
            }
            *result = CreateSuccess(file);
        }
        catch (Exception e)
        {
            *result = CreateErrorWithoutThrowing(e);
        }
    }

    /// <summary>
    /// Creates the failed result for an exception, falling back to a result without text when even that fails, so no
    /// exception escapes to native code.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The failed result.</returns>
    private static UnmanagedSimaiParseResult CreateErrorWithoutThrowing(Exception exception)
    {
        try
        {
            return CreateError(exception);
        }
        catch
        {
            return new UnmanagedSimaiParseResult { code = UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR };
        }
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
