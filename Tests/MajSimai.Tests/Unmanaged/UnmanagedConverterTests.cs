using System;
using System.Collections.Generic;
using System.Linq;
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
    /// A file with commands, several charts, a BPM and speed change, slides with related notes and non-ASCII text.
    /// </summary>
    private const string RichFile = "&title=星になれ\n&artist=Aé\n&wholebpm=1\n&des=X\n"
                                    + "&inote_1=(120){4}1-3[8:1]*-5[8:1]/2,<HS*2>3h[4:1],,{8}B1f,E\n"
                                    + "&inote_5=(180)1,2,||comment\n3b,\n";

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
            Assert.Equal("Song", Marshal.PtrToStringUTF8((nint)native->title, native->titleLen));
            Assert.Equal(7, native->chartsLen);
            Assert.Equal(1, native->commandsLen);
            Assert.Equal("wholebpm", Marshal.PtrToStringUTF8((nint)native->commands[0].prefix, native->commands[0].prefixLen));
            var chart = native->charts[0];
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
            Assert.True(native->charts[1].isEmpty);
            Assert.True(native->charts[1].noteTimings is null);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// Texts: are copied as zero-terminated UTF-8 with lengths in bytes.
    /// </summary>
    [Fact]
    public void Texts_AreCopiedAsZeroTerminatedUtf8WithLengthsInBytes()
    {
        var file = SimaiParser.Parse("&title=星になれ\n&artist=Aé\n&inote_1=1,".AsSpan(), string.Empty);
        var native = UnmanagedConverter.ToUnmanaged(file);
        try
        {
            Assert.Equal(12, native->titleLen);
            Assert.Equal("星になれ", Marshal.PtrToStringUTF8((nint)native->title, native->titleLen));
            Assert.Equal(0, native->title[native->titleLen]);
            Assert.Equal(3, native->artistLen);
            Assert.True(native->finalDesigner is null);
            Assert.Equal(0, native->finalDesignerLen);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// File: is copied into one block that starts with the file: every part lies inside the counted size, is aligned
    /// and overlaps no other part.
    /// </summary>
    [Fact]
    public void File_IsCopiedIntoOneBlockOfAlignedPartsThatDoNotOverlap()
    {
        var file = SimaiParser.Parse(RichFile.AsSpan(), "h");
        var size = UnmanagedConverter.SizeOf(file);
        var native = UnmanagedConverter.ToUnmanaged(file);
        try
        {
            var start = (long)native;
            var parts = Parts(native).OrderBy(part => part.Start).ToArray();

            Assert.Equal(start, parts[0].Start);
            Assert.All(parts, part => Assert.InRange(part.Start - start, 0, size - part.Length));
            Assert.All(parts, part => Assert.Equal(0, (part.Start - start) % NativeBlockWriter.Alignment));
            for (var i = 1; i < parts.Length; i++)
            {
                Assert.True(parts[i].Start >= parts[i - 1].Start + parts[i - 1].Length, $"Part {i} overlaps the part before it.");
            }
            Assert.True(parts.Length > 20);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// Comma timing points: are copied from their values without creating their objects, with the values the objects
    /// have, and the same once the objects exist.
    /// </summary>
    [Fact]
    public void CommaTimings_AreCopiedFromTheirValuesWithoutCreatingTheirObjects()
    {
        var chart = SimaiParser.ParseChart("(120){4}1,<HS*2>,{8}(150)2,,<SV*0.5>3,".AsSpan());
        Assert.True(chart.TryGetCommaData(out var data));
        var fromValues = UnmanagedConverter.ToUnmanaged(File(chart));
        try
        {
            Assert.True(chart.TryGetCommaData(out _), "Copying the chart created the comma timing point objects.");
            var objects = chart.CommaTimings;
            Assert.False(chart.TryGetCommaData(out var noData));
            Assert.Equal(0, noData.Length);
            var fromObjects = UnmanagedConverter.ToUnmanaged(File(chart));
            try
            {
                var copied = fromValues->charts[0];
                Assert.Equal(data.Length, copied.commaTimingsLen);
                Assert.Equal(objects.Length, copied.commaTimingsLen);
                for (var i = 0; i < objects.Length; i++)
                {
                    var comma = copied.commaTimings[i];
                    Assert.Equal(objects[i].Timing, comma.timing);
                    Assert.Equal(objects[i].Bpm, comma.bpm);
                    Assert.Equal(objects[i].HSpeed, comma.hSpeed);
                    Assert.Equal(objects[i].SVeloc, comma.sVeloc);
                    Assert.Equal(objects[i].RawTextPositionX, comma.rawTextPositionX);
                    Assert.Equal(objects[i].RawTextPositionY, comma.rawTextPositionY);
                    Assert.Equal(objects[i].RawTextPosition, comma.rawTextPosition);
                    Assert.True(comma.isEmpty);
                    Assert.True(comma.notes is null && comma.notesLen == 0);
                    Assert.True(comma.rawContent is null && comma.rawContentLen == 0);
                    Assert.Equal(Bytes(&comma), Bytes(fromObjects->charts[0].commaTimings + i));
                }
                Assert.Contains(objects.ToArray(), timing => timing.HSpeed == 2f);
                Assert.Contains(objects.ToArray(), timing => timing.Bpm == 150f);
            }
            finally
            {
                Marshal.FreeHGlobal((nint)fromObjects);
            }
        }
        finally
        {
            Marshal.FreeHGlobal((nint)fromValues);
        }
    }

    /// <summary>
    /// Comma timing points: of a chart created with timing point objects, are copied from the objects, notes and text included.
    /// </summary>
    [Fact]
    public void CommaTimings_OfAChartCreatedWithObjects_AreCopiedFromTheObjects()
    {
        var tap = new SimaiNote { Type = SimaiNoteType.Tap, StartPosition = 1, RawContent = "1" };
        var comma = new SimaiTimingPoint(0.5, new[] { tap }, "1".AsSpan(), bpm: 120f);
        var chart = new SimaiChart("13", "D", "1,", ReadOnlySpan<SimaiTimingPoint>.Empty, new[] { comma });

        Assert.False(chart.TryGetCommaData(out _));
        var native = UnmanagedConverter.ToUnmanaged(File(chart));
        try
        {
            var copied = native->charts[0].commaTimings[0];
            Assert.Equal(1, native->charts[0].commaTimingsLen);
            Assert.Equal(0.5, copied.timing);
            Assert.Equal(1, copied.notesLen);
            Assert.Equal("1", Marshal.PtrToStringUTF8((nint)copied.rawContent, copied.rawContentLen));
            Assert.Equal("1", Marshal.PtrToStringUTF8((nint)copied.notes[0].rawContent, copied.notes[0].rawContentLen));
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// Related note outside the timing point: is minus one.
    /// </summary>
    [Fact]
    public void RelatedNoteOutsideTheTimingPoint_IsMinusOne()
    {
        var note = new SimaiNote { RelatedNotes = new[] { new SimaiNote() } };
        var timing = new SimaiTimingPoint(0, new[] { note }, ReadOnlySpan<char>.Empty);
        var native = UnmanagedConverter.ToUnmanaged(File(new SimaiChart(string.Empty, string.Empty, "1", new[] { timing })));
        try
        {
            var copied = native->charts[0].noteTimings[0].notes[0];
            Assert.Equal(1, copied.relatedNotesLen);
            Assert.Equal(-1, copied.relatedNotes[0]);
            Assert.True(copied.rawContent is null);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// Size: matches the parts written for files of the samples, so the block is filled exactly.
    /// </summary>
    /// <param name="content">The file.</param>
    [Theory]
    [InlineData("&inote_1=1,")]
    [InlineData("&title=星\n")]
    [InlineData(RichFile)]
    public void SizeOf_CountsEveryPartOfTheBlock(string content)
    {
        var file = SimaiParser.Parse(content.AsSpan(), "h");
        var native = UnmanagedConverter.ToUnmanaged(file);
        try
        {
            var end = Parts(native).Max(part => part.Start + Align(part.Length));

            Assert.Equal(UnmanagedConverter.SizeOf(file), end - (long)native);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)native);
        }
    }

    /// <summary>
    /// Parse result: frees file.
    /// </summary>
    [Fact]
    public void ParseResult_FreesFile()
    {
        var file = SimaiParser.Parse("&inote_1=1,".AsSpan(), string.Empty);
        var result = new UnmanagedSimaiParseResult { code = UnmanagedSimaiParseResult.CODE_NO_ERROR, simaiFile = UnmanagedConverter.ToUnmanaged(file) };

        result.Free();

        Assert.True(result.simaiFile is null);
        Assert.Equal(-1, result.code);
    }

    /// <summary>
    /// Creates a file with one chart.
    /// </summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The file.</returns>
    private static SimaiFile File(SimaiChart chart)
    {
        return new SimaiFile("T", "A", 0f, "h", new[] { chart }, null);
    }

    /// <summary>
    /// Gets the bytes of a native timing point.
    /// </summary>
    /// <param name="timing">The timing point.</param>
    /// <returns>Its bytes.</returns>
    private static byte[] Bytes(UnmanagedSimaiTimingPoint* timing)
    {
        return new ReadOnlySpan<byte>(timing, sizeof(UnmanagedSimaiTimingPoint)).ToArray();
    }

    /// <summary>
    /// Rounds a size up to the alignment of the block.
    /// </summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The rounded size.</returns>
    private static long Align(long size)
    {
        return (size + NativeBlockWriter.Alignment - 1) / NativeBlockWriter.Alignment * NativeBlockWriter.Alignment;
    }

    /// <summary>
    /// Lists every part of a native file: the start and length in bytes of the file itself and of every text and array
    /// it points to, texts with their zero byte.
    /// </summary>
    /// <param name="file">The native file.</param>
    /// <returns>The parts, in no particular order.</returns>
    private static List<(long Start, long Length)> Parts(UnmanagedSimaiFile* file)
    {
        var parts = new List<(long Start, long Length)> { ((long)file, sizeof(UnmanagedSimaiFile)) };
        AddText(parts, file->title, file->titleLen);
        AddText(parts, file->artist, file->artistLen);
        AddText(parts, file->finalDesigner, file->finalDesignerLen);
        AddArray(parts, file->charts, file->chartsLen, sizeof(UnmanagedSimaiChart));
        AddArray(parts, file->commands, file->commandsLen, sizeof(UnmanagedSimaiCommand));
        for (var c = 0; c < file->commandsLen; c++)
        {
            AddText(parts, file->commands[c].prefix, file->commands[c].prefixLen);
            AddText(parts, file->commands[c].value, file->commands[c].valueLen);
        }
        for (var c = 0; c < file->chartsLen; c++)
        {
            var chart = file->charts[c];
            AddText(parts, chart.level, chart.levelLen);
            AddText(parts, chart.designer, chart.designerLen);
            AddText(parts, chart.fumen, chart.fumenLen);
            AddTimings(parts, chart.noteTimings, chart.noteTimingsLen);
            AddTimings(parts, chart.commaTimings, chart.commaTimingsLen);
        }

        return parts;
    }

    /// <summary>
    /// Adds the parts of an array of timing points and of their notes.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="timings">The timing points.</param>
    /// <param name="length">The number of timing points.</param>
    private static void AddTimings(List<(long Start, long Length)> parts, UnmanagedSimaiTimingPoint* timings, int length)
    {
        AddArray(parts, timings, length, sizeof(UnmanagedSimaiTimingPoint));
        for (var t = 0; t < length; t++)
        {
            AddText(parts, timings[t].rawContent, timings[t].rawContentLen);
            AddArray(parts, timings[t].notes, timings[t].notesLen, sizeof(UnmanagedSimaiNote));
            for (var n = 0; n < timings[t].notesLen; n++)
            {
                AddText(parts, timings[t].notes[n].rawContent, timings[t].notes[n].rawContentLen);
                AddArray(parts, timings[t].notes[n].relatedNotes, timings[t].notes[n].relatedNotesLen, sizeof(int));
            }
        }
    }

    /// <summary>
    /// Adds a text, with its zero byte, unless it is null.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="text">The text.</param>
    /// <param name="length">Its length in bytes, without the zero byte.</param>
    private static void AddText(List<(long Start, long Length)> parts, byte* text, int length)
    {
        if (text is not null)
        {
            parts.Add(((long)text, length + 1));
        }
    }

    /// <summary>
    /// Adds an array, unless it is null.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <param name="array">The array.</param>
    /// <param name="length">The number of items.</param>
    /// <param name="itemSize">The size of an item in bytes.</param>
    private static void AddArray(List<(long Start, long Length)> parts, void* array, int length, int itemSize)
    {
        if (array is not null)
        {
            parts.Add(((long)array, (long)length * itemSize));
        }
    }
}
