using System;
using MajSimai.Unmanaged;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// Parses a file through the native entry points <c>MajSimai_ParseWithTolerance</c> and <c>MajSimai_Free</c>, called
/// in-process through function pointers. It is the work the C++ wrapper asks of MajSimai, without the C++ side: the
/// parse, the copy of the result into native memory, and freeing it.
/// </summary>
internal static unsafe class NativeAbiParser
{
    /// <summary>
    /// Parses a file in lenient mode into a native result and frees the result.
    /// </summary>
    /// <param name="file">The bytes of the file; an empty file is passed on and rejected by the entry point.</param>
    /// <returns><see langword="true"/> when the entry point returned a parsed file, <see langword="false"/> when it returned an error.</returns>
    public static bool Parse(ArraySegment<byte> file)
    {
        delegate* unmanaged[Cdecl]<byte*, int, SimaiSyntaxTolerance, UnmanagedSimaiParseResult*, void> parse = &UnmanagedSimaiParser.ParseWithTolerance;
        delegate* unmanaged[Cdecl]<UnmanagedSimaiParseResult*, int> free = &UnmanagedSimaiParser.Free;
        var result = new UnmanagedSimaiParseResult { code = -1 };

        fixed (byte* content = file.AsSpan())
        {
            parse(content, file.Count, SimaiSyntaxTolerance.Lenient, &result);
        }
        var parsed = result.code == UnmanagedSimaiParseResult.CODE_NO_ERROR && result.simaiFile != null;
        free(&result);
        return parsed;
    }
}
