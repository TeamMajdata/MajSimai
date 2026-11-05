using System;
using System.Runtime.InteropServices;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.Unmanaged;

/// <summary>
/// Tests copying results into native memory.
/// </summary>
public sealed unsafe class UnmanagedConverterTests
{
    /// <summary>
    /// File: is copied with related note indices.
    /// </summary>
    [Fact]
    public void File_IsCopiedWithRelatedNoteIndices()
    {
        var file = SimaiParser.Parse("&title=Song\n&wholebpm=1\n&inote_1=(120)1-3[8:1]*-5[8:1]/2,".AsSpan(), "h");
        var native = UnmanagedConverter.ToUnmanaged(file);
        try
        {
            Assert.Equal("Song", Marshal.PtrToStringUTF8((nint)native.title, native.titleLen));
            Assert.Equal(7, native.chartsLen);
            Assert.Equal(1, native.commandsLen);
            Assert.Equal("wholebpm", Marshal.PtrToStringUTF8((nint)native.commands[0].prefix, native.commands[0].prefixLen));
            var chart = native.charts[0];
            Assert.Equal(1, chart.noteTimingsLen);
            var timing = chart.noteTimings[0];
            Assert.Equal(4, timing.notesLen);
            var head = timing.notes[0];
            Assert.Equal(SimaiNoteType.Tap, head.type);
            Assert.True(head.isStar);
            Assert.True(head.isStarDouble);
            Assert.True(head.isEach);
            Assert.Equal(2, head.relatedNotesLen);
            Assert.Equal(1, head.relatedNotes[0]);
            Assert.Equal(2, head.relatedNotes[1]);
            Assert.Equal("1-5[8:1]", Marshal.PtrToStringUTF8((nint)timing.notes[2].rawContent, timing.notes[2].rawContentLen));
            Assert.True(native.charts[1].isEmpty);
            Assert.True(native.charts[1].noteTimings is null);
        }
        finally
        {
            native.Free();
        }
        Assert.True(native.charts is null);
        Assert.True(native.title is null);
    }

    /// <summary>
    /// Texts: are copied as zero-terminated UTF-8 with lengths in bytes.
    /// </summary>
    [Fact]
    public void Texts_AreCopiedAsZeroTerminatedUtf8WithLengthsInBytes()
    {
        var file = SimaiParser.Parse("&title=\u661f\u306b\u306a\u308c\n&artist=A\u00e9\n&inote_1=1,".AsSpan(), string.Empty);
        var native = UnmanagedConverter.ToUnmanaged(file);
        try
        {
            Assert.Equal(12, native.titleLen);
            Assert.Equal("\u661f\u306b\u306a\u308c", Marshal.PtrToStringUTF8((nint)native.title, native.titleLen));
            Assert.Equal(0, native.title[native.titleLen]);
            Assert.Equal(3, native.artistLen);
            Assert.True(native.finalDesigner is null);
            Assert.Equal(0, native.finalDesignerLen);
        }
        finally
        {
            native.Free();
        }
    }

    /// <summary>
    /// Related note outside the timing point: is minus one.
    /// </summary>
    [Fact]
    public void RelatedNoteOutsideTheTimingPoint_IsMinusOne()
    {
        var note = new SimaiNote { RelatedNotes = new[] { new SimaiNote() } };
        var native = UnmanagedConverter.ToUnmanaged(note, new[] { note });
        try
        {
            Assert.Equal(-1, native.relatedNotes[0]);
            Assert.True(native.rawContent is null);
        }
        finally
        {
            native.Free();
        }
    }

    /// <summary>
    /// Parse result: frees file.
    /// </summary>
    [Fact]
    public void ParseResult_FreesFile()
    {
        var file = SimaiParser.Parse("&inote_1=1,".AsSpan(), string.Empty);
        var pointer = (UnmanagedSimaiFile*)Marshal.AllocHGlobal(sizeof(UnmanagedSimaiFile));
        *pointer = UnmanagedConverter.ToUnmanaged(file);
        var result = new UnmanagedSimaiParseResult { code = UnmanagedSimaiParseResult.CODE_NO_ERROR, simaiFile = pointer };

        result.Free();

        Assert.True(result.simaiFile is null);
        Assert.Equal(-1, result.code);
    }
}
