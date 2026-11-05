using System;
using System.Runtime.InteropServices;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.Unmanaged;

/// <summary>
/// Tests handing out the parts of one block of native memory.
/// </summary>
public sealed unsafe class NativeBlockWriterTests
{
    /// <summary>
    /// Allocate: hands out aligned parts one after the other, and nothing for no items.
    /// </summary>
    [Fact]
    public void Allocate_HandsOutAlignedPartsInOrder()
    {
        var block = stackalloc long[8];
        var writer = new NativeBlockWriter((byte*)block, 64);

        var ints = writer.Allocate<int>(3);
        var none = writer.Allocate<double>(0);
        var doubles = writer.Allocate<double>(2);

        Assert.True(ints == (int*)block);
        Assert.True(none is null);
        Assert.True((byte*)doubles == (byte*)block + 16);
        Assert.Equal(32, writer.Used);
        Assert.Equal(16, NativeBlockWriter.SizeOfArray<int>(3));
        Assert.Equal(0, NativeBlockWriter.SizeOfArray<double>(0));
        Assert.Equal(16, NativeBlockWriter.SizeOfArray<double>(2));
    }

    /// <summary>
    /// WriteUtf8: writes zero-terminated UTF-8, takes its rounded size, and nothing for a null or empty text.
    /// </summary>
    [Fact]
    public void WriteUtf8_WritesZeroTerminatedUtf8()
    {
        var block = stackalloc long[8];
        var writer = new NativeBlockWriter((byte*)block, 64);

        var text = writer.WriteUtf8("星A", out var length);
        var empty = writer.WriteUtf8(string.Empty, out var emptyLength);
        var missing = writer.WriteUtf8(null, out var missingLength);
        var next = writer.Allocate<byte>(1);

        Assert.Equal(4, length);
        Assert.Equal("星A", Marshal.PtrToStringUTF8((nint)text, length));
        Assert.Equal(0, text[length]);
        Assert.True(empty is null && emptyLength == 0);
        Assert.True(missing is null && missingLength == 0);
        Assert.True(next == text + 8);
        Assert.Equal(8, NativeBlockWriter.SizeOfUtf8("星A"));
        Assert.Equal(0, NativeBlockWriter.SizeOfUtf8(string.Empty));
        Assert.Equal(0, NativeBlockWriter.SizeOfUtf8(null));
        Assert.Equal(16, writer.Used);
    }

    /// <summary>
    /// The writer: throws instead of writing past the end of the block, and keeps what it handed out.
    /// </summary>
    [Fact]
    public void Writer_ThrowsInsteadOfWritingPastTheBlock()
    {
        var block = (byte*)Marshal.AllocHGlobal(16);
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var writer = new NativeBlockWriter(block, 16);
                writer.Allocate<long>(2);
                writer.Allocate<byte>(1);
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                var writer = new NativeBlockWriter(block, 16);
                writer.WriteUtf8("sixteen bytes!!!", out _);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var writer = new NativeBlockWriter(block, 16);
                writer.Allocate<int>(-1);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var writer = new NativeBlockWriter(block, -1);
            });
            var full = new NativeBlockWriter(block, 16);
            full.Allocate<long>(2);
            Assert.Equal(16, full.Used);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)block);
        }
    }
}
