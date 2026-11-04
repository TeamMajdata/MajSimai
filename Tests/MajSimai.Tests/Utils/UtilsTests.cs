using System;
using MajSimai.Utils;
using Xunit;

namespace MajSimai.Tests.Utils;

/// <summary>
/// Tests the internal helpers <see cref="PooledBuffer{T}"/>, <see cref="StringCache"/> and <see cref="LegacySplit"/>.
/// </summary>
public sealed class UtilsTests
{
    /// <summary>
    /// Pooled buffer: grows and copies.
    /// </summary>
    [Fact]
    public void PooledBuffer_GrowsAndCopies()
    {
        using var buffer = new PooledBuffer<int>(1);
        for (var i = 0; i < 100; i++)
        {
            buffer.Add(i);
        }
        buffer.AddRange(new[] { 100, 101 });

        Assert.Equal(102, buffer.Count);
        Assert.Equal(50, buffer[50]);
        buffer[50] = -1;
        Assert.Equal(-1, buffer.AsSpan()[50]);
        Assert.Equal(new[] { 100, 101 }, buffer.AsSpan(100, 2).ToArray());
        Assert.Equal(102, buffer.ToArray().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[102]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
    }

    /// <summary>
    /// Pooled buffer: clear and dispose keep it usable.
    /// </summary>
    [Fact]
    public void PooledBuffer_ClearAndDisposeKeepItUsable()
    {
        var buffer = new PooledBuffer<string>();
        buffer.Add("a");
        buffer.Clear();
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.ToArray());
        buffer.Dispose();
        buffer.Add("b");
        Assert.Equal("b", buffer[0]);
        buffer.Dispose();
        buffer.Dispose();
        Assert.Throws<ArgumentOutOfRangeException>(() => new PooledBuffer<int>(-1));
    }

    /// <summary>
    /// String cache: reuses short strings.
    /// </summary>
    [Fact]
    public void StringCache_ReusesShortStrings()
    {
        var first = StringCache.Get("1-5[8:1]".AsSpan());
        var second = StringCache.Get("1-5[8:1]".ToCharArray());

        Assert.Same(first, second);
        Assert.Same(string.Empty, StringCache.Get(ReadOnlySpan<char>.Empty));
        var longText = new string('1', StringCache.MaxCachedLength + 1);
        Assert.Equal(longText, StringCache.Get(longText.AsSpan()));
        Assert.NotSame(StringCache.Get(longText.AsSpan()), StringCache.Get(longText.AsSpan()));
    }

    /// <summary>
    /// String cache: returns equal text for every input.
    /// </summary>
    [Fact]
    public void StringCache_ReturnsEqualTextForEveryInput()
    {
        for (var i = 0; i < StringCache.SlotCount * 2; i++)
        {
            var text = i.ToString();
            Assert.Equal(text, StringCache.Get(text.AsSpan()));
        }
    }

    /// <summary>
    /// Legacy split: stops when full.
    /// </summary>
    /// <param name="text">A test case value.</param>
    /// <param name="separator">A test case value.</param>
    /// <param name="capacity">A test case value.</param>
    /// <param name="removeEmpty">A test case value.</param>
    /// <param name="expected">A test case value.</param>
    [Theory]
    [InlineData("a:b", ':', 2, false, new[] { "a", "b" })]
    [InlineData("a:b:c", ':', 2, false, new[] { "a", "b" })]
    [InlineData("a::b", ':', 4, true, new[] { "a", "b" })]
    [InlineData("a::b", ':', 4, false, new[] { "a", "", "b" })]
    [InlineData("", ':', 2, false, new[] { "" })]
    [InlineData("", ':', 2, true, new string[0])]
    [InlineData(":", ':', 2, true, new string[0])]
    [InlineData("abc", ':', 0, false, new string[0])]
    [InlineData("a:", ':', 2, false, new[] { "a", "" })]
    public void LegacySplit_StopsWhenFull(string text, char separator, int capacity, bool removeEmpty, string[] expected)
    {
        var ranges = new Range[capacity];
        var count = LegacySplit.Split(text.AsSpan(), ranges, separator, removeEmpty);

        Assert.Equal(expected.Length, count);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], text[ranges[i]]);
        }
    }

    /// <summary>
    /// Legacy split: counts characters.
    /// </summary>
    [Fact]
    public void LegacySplit_CountsCharacters()
    {
        Assert.Equal(3, LegacySplit.Count("a`b`c`".AsSpan(), '`'));
        Assert.Equal(0, LegacySplit.Count(ReadOnlySpan<char>.Empty, '`'));
    }
}
