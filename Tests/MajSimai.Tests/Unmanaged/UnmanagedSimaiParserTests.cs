using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.Unmanaged;

/// <summary>
/// Tests the native entry points by calling them through unmanaged function pointers, the way native code calls them.
/// </summary>
public sealed unsafe class UnmanagedSimaiParserTests
{
    /// <summary>
    /// Parse: reads UTF-8 content and fills the result.
    /// </summary>
    [Fact]
    public void Parse_ReadsUtf8ContentAndFillsTheResult()
    {
        var result = Parse(Encoding.UTF8.GetBytes("&title=星になれ\n&inote_2=(120)1,2,"));
        try
        {
            Assert.Equal(UnmanagedSimaiParseResult.CODE_NO_ERROR, result.code);
            Assert.True(result.errorMsg is null);
            var file = (UnmanagedSimaiFile*)result.simaiFile;
            Assert.Equal("星になれ", Marshal.PtrToStringUTF8((nint)file->title, file->titleLen));
            Assert.Equal(2, file->charts[1].noteTimingsLen);
        }
        finally
        {
            Assert.Equal(1, Free(&result));
        }
        Assert.True(result.simaiFile is null);
    }

    /// <summary>
    /// Parse: skips a UTF-8 byte order mark.
    /// </summary>
    [Fact]
    public void Parse_SkipsAUtf8ByteOrderMark()
    {
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("&title=A")).ToArray();
        var result = Parse(content);
        try
        {
            var file = (UnmanagedSimaiFile*)result.simaiFile;
            Assert.Equal("A", Marshal.PtrToStringUTF8((nint)file->title, file->titleLen));
        }
        finally
        {
            Free(&result);
        }
    }

    /// <summary>
    /// Parse: reports empty content as invalid content.
    /// </summary>
    [Fact]
    public void Parse_ReportsEmptyContentAsInvalidContent()
    {
        var result = Parse(Array.Empty<byte>());
        try
        {
            Assert.Equal(UnmanagedSimaiParseResult.CODE_INVALID_CONTENT, result.code);
            Assert.True(result.simaiFile is null);
            Assert.Equal("The content is empty.", Marshal.PtrToStringUTF8((nint)result.errorMsg, result.errorMsgLen));
            Assert.Equal(-1, result.errorAtLine);
        }
        finally
        {
            Free(&result);
        }
    }

    /// <summary>
    /// Parse: ignores a null result pointer.
    /// </summary>
    [Fact]
    public void Parse_IgnoresANullResultPointer()
    {
        var content = stackalloc byte[] { (byte)'1' };
        delegate* unmanaged[Cdecl]<byte*, int, UnmanagedSimaiParseResult*, void> parse = &UnmanagedSimaiParser.Parse;

        parse(content, 1, null);
    }

    /// <summary>
    /// Create error: maps simai exceptions to their codes with the position and the content.
    /// </summary>
    /// <param name="syntax">Whether the exception is a syntax exception rather than a note exception.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(true, UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX)]
    [InlineData(false, UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP)]
    public void CreateError_MapsSimaiExceptionsToTheirCodes(bool syntax, int code)
    {
        Exception exception = syntax
            ? new InvalidSimaiSyntaxException(3, 7, "(120", "The BPM declaration is not closed.")
            : new InvalidSimaiNoteException(3, 7, "星", "The note has no button.");

        var result = UnmanagedSimaiParser.CreateError(exception);
        try
        {
            Assert.Equal(code, result.code);
            Assert.Equal(3, result.errorAtLine);
            Assert.Equal(7, result.errorAtColumn);
            Assert.Equal(exception.Message, Marshal.PtrToStringUTF8((nint)result.errorMsg, result.errorMsgLen));
            Assert.Equal(((InvalidSimaiMarkupException)exception).Content, Marshal.PtrToStringUTF8((nint)result.errorContent, result.errorContentLen));
        }
        finally
        {
            result.Free();
        }
    }

    /// <summary>
    /// Create error: reports other exceptions as internal errors with the full exception text.
    /// </summary>
    [Fact]
    public void CreateError_ReportsOtherExceptionsAsInternalErrors()
    {
        var result = UnmanagedSimaiParser.CreateError(new InvalidOperationException("broken"));
        try
        {
            Assert.Equal(UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR, result.code);
            Assert.Contains("System.InvalidOperationException: broken", Marshal.PtrToStringUTF8((nint)result.errorMsg, result.errorMsgLen));
            Assert.True(result.errorContent is null);
            Assert.Equal(-1, result.errorAtLine);
        }
        finally
        {
            result.Free();
        }
    }

    /// <summary>
    /// Parse with tolerance: reports the first strict error with its code, position and content.
    /// </summary>
    /// <param name="chart">The chart text.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData("(120)1,{4", UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_SYNTAX)]
    [InlineData("(120)1/b/2,", UnmanagedSimaiParseResult.CODE_INVALID_SIMAI_MARKUP)]
    public void ParseWithTolerance_ReportsTheFirstStrictError(string chart, int code)
    {
        var text = "&title=Strict\n&inote_2=" + chart;
        var expected = Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.Parse(text.AsSpan(), string.Empty, SimaiParserOptions.Strict));

        var result = ParseWithTolerance(Encoding.UTF8.GetBytes(text), SimaiSyntaxTolerance.Strict);
        try
        {
            Assert.Equal(code, result.code);
            Assert.True(result.simaiFile is null);
            Assert.Equal(expected.Line, result.errorAtLine);
            Assert.Equal(expected.Column, result.errorAtColumn);
            Assert.Equal(expected.Message, Marshal.PtrToStringUTF8((nint)result.errorMsg, result.errorMsgLen));
            Assert.Equal(expected.Content, Marshal.PtrToStringUTF8((nint)result.errorContent, result.errorContentLen));
        }
        finally
        {
            Assert.Equal(1, Free(&result));
        }
    }

    /// <summary>
    /// Parse with tolerance: keeps a failed chart in lenient mode, like MajSimai_Parse.
    /// </summary>
    [Fact]
    public void ParseWithTolerance_KeepsAFailedChartInLenientMode()
    {
        var result = ParseWithTolerance(Encoding.UTF8.GetBytes("&inote_1=(120)1,{4\n&inote_2=(120)1,"), SimaiSyntaxTolerance.Lenient);
        try
        {
            Assert.Equal(UnmanagedSimaiParseResult.CODE_NO_ERROR, result.code);
            var file = (UnmanagedSimaiFile*)result.simaiFile;
            Assert.True(file->charts[0].isEmpty);
            Assert.Equal(1, file->charts[1].noteTimingsLen);
        }
        finally
        {
            Free(&result);
        }
    }

    /// <summary>
    /// Parse with tolerance: rejects an unknown tolerance.
    /// </summary>
    [Fact]
    public void ParseWithTolerance_RejectsAnUnknownTolerance()
    {
        var result = ParseWithTolerance(Encoding.UTF8.GetBytes("&inote_1=1,"), (SimaiSyntaxTolerance)7);
        try
        {
            Assert.Equal(UnmanagedSimaiParseResult.CODE_INVALID_ARGUMENT, result.code);
            Assert.True(result.simaiFile is null);
            Assert.Equal("The tolerance 7 is neither lenient (0) nor strict (1).", Marshal.PtrToStringUTF8((nint)result.errorMsg, result.errorMsgLen));
        }
        finally
        {
            Free(&result);
        }
    }

    /// <summary>
    /// Free: returns 1 when it freed something and 0 for null pointers, and a freed result can be freed again.
    /// </summary>
    [Fact]
    public void Free_ReturnsOneWhenItFreedSomethingAndZeroForNullPointers()
    {
        delegate* unmanaged[Cdecl]<nint, int> freeHGlobal = &UnmanagedSimaiParser.Free;
        var result = Parse(Encoding.UTF8.GetBytes("&inote_1=1,"));

        Assert.Equal(1, Free(&result));
        Assert.Equal(1, Free(&result));
        Assert.True(result.simaiFile is null);
        Assert.Equal(-1, result.code);
        Assert.Equal(0, Free(null));
        Assert.Equal(0, freeHGlobal(0));
        Assert.Equal(1, freeHGlobal(Marshal.AllocHGlobal(4)));
    }

    /// <summary>
    /// Calls <c>MajSimai_Parse</c> through an unmanaged function pointer.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The result; free it with <see cref="Free"/>.</returns>
    private static UnmanagedSimaiParseResult Parse(byte[] content)
    {
        delegate* unmanaged[Cdecl]<byte*, int, UnmanagedSimaiParseResult*, void> parse = &UnmanagedSimaiParser.Parse;
        var result = new UnmanagedSimaiParseResult { code = -1 };
        fixed (byte* pointer = content)
        {
            parse(pointer, content.Length, &result);
        }

        return result;
    }

    /// <summary>
    /// Calls <c>MajSimai_ParseWithTolerance</c> through an unmanaged function pointer.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="tolerance">The syntax tolerance.</param>
    /// <returns>The result; free it with <see cref="Free"/>.</returns>
    private static UnmanagedSimaiParseResult ParseWithTolerance(byte[] content, SimaiSyntaxTolerance tolerance)
    {
        delegate* unmanaged[Cdecl]<byte*, int, SimaiSyntaxTolerance, UnmanagedSimaiParseResult*, void> parse = &UnmanagedSimaiParser.ParseWithTolerance;
        var result = new UnmanagedSimaiParseResult { code = -1 };
        fixed (byte* pointer = content)
        {
            parse(pointer, content.Length, tolerance, &result);
        }

        return result;
    }

    /// <summary>
    /// Calls <c>MajSimai_Free</c> through an unmanaged function pointer.
    /// </summary>
    /// <param name="result">The result to free.</param>
    /// <returns>What <c>MajSimai_Free</c> returned.</returns>
    private static int Free(UnmanagedSimaiParseResult* result)
    {
        delegate* unmanaged[Cdecl]<UnmanagedSimaiParseResult*, int> free = &UnmanagedSimaiParser.Free;

        return free(result);
    }
}
