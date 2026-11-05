using System;
using System.Collections.Generic;
using Xunit;

namespace MajSimai.Tests.Model;

/// <summary>
/// Tests the public constructors and members of the result types.
/// </summary>
public sealed class ModelTypeTests
{
    /// <summary>
    /// Timing point: removes whitespace and keeps values.
    /// </summary>
    [Fact]
    public void TimingPoint_RemovesWhitespaceAndKeepsValues()
    {
        var notes = new[] { new SimaiNote() };
        var timing = new SimaiTimingPoint(1.5, notes, " 1 /\n2\t".AsSpan(), 3, 4, 120f, 2f, 0.5f, 7, 3, 8);

        Assert.Equal("1/2", timing.RawContent);
        Assert.Same(notes, timing.Notes);
        Assert.Equal(1.5, timing.Timing);
        Assert.Equal(3, timing.RawTextPositionX);
        Assert.Equal(4, timing.RawTextPositionY);
        Assert.Equal(120f, timing.Bpm);
        Assert.Equal(2f, timing.HSpeed);
        Assert.Equal(0.5f, timing.SVeloc);
        Assert.Equal(7, timing.RawTextPosition);
        Assert.Equal(3, timing.SignatureNumerator);
        Assert.Equal(8, timing.SignatureDenominator);
        Assert.False(timing.IsEmpty);
    }

    /// <summary>
    /// Timing point: defaults and setters.
    /// </summary>
    [Fact]
    public void TimingPoint_DefaultsAndSetters()
    {
        var timing = new SimaiTimingPoint(0, null, "abc".AsSpan());

        Assert.True(timing.IsEmpty);
        Assert.Equal("abc", timing.RawContent);
        Assert.Equal(1f, timing.HSpeed);
        Assert.Equal(1f, timing.SVeloc);
        timing.Notes = new[] { new SimaiNote() };
        timing.Timing = 2;
        timing.Bpm = 90;
        timing.HSpeed = 3;
        timing.SVeloc = 4;
        Assert.False(timing.IsEmpty);
        Assert.Equal((2d, 90f, 3f, 4f), (timing.Timing, timing.Bpm, timing.HSpeed, timing.SVeloc));
        Assert.Throws<ArgumentNullException>(() => timing.Notes = null!);
        Assert.Equal(string.Empty, new SimaiTimingPoint(0, null, ReadOnlySpan<char>.Empty).RawContent);
    }

    /// <summary>
    /// Chart: copies timing points.
    /// </summary>
    [Fact]
    public void Chart_CopiesTimingPoints()
    {
        var timings = new[] { new SimaiTimingPoint(0, null, "1".AsSpan()) };
        var commas = new[] { new SimaiTimingPoint(0, null, ReadOnlySpan<char>.Empty) };
        var chart = new SimaiChart("1", "d", "1,", timings, commas);
        timings[0] = null!;

        Assert.NotNull(chart.NoteTimings[0]);
        Assert.Single(chart.CommaTimings.ToArray());
        Assert.False(chart.IsEmpty);
        Assert.True(new SimaiChart("1", "d", string.Empty, ReadOnlySpan<SimaiTimingPoint>.Empty).IsEmpty);
    }

    /// <summary>
    /// Chart: rejects invalid arguments.
    /// </summary>
    [Fact]
    public void Chart_RejectsInvalidArguments()
    {
        var timings = new[] { new SimaiTimingPoint(0, null, "1".AsSpan()) };

        Assert.Throws<ArgumentNullException>(() => new SimaiChart(null!, "d", "1,", timings));
        Assert.Throws<ArgumentNullException>(() => new SimaiChart("1", null!, "1,", timings));
        Assert.Throws<ArgumentException>(() => new SimaiChart("1", "d", string.Empty, timings));
        Assert.Throws<ArgumentException>(() => new SimaiChart("1", "d", "1,", new SimaiTimingPoint[] { null! }));
    }

    /// <summary>
    /// Parsed chart: creates comma timings once across threads.
    /// </summary>
    [Fact]
    public void ParsedChart_CreatesCommaTimingsOnceAcrossThreads()
    {
        var chart = SimaiParser.ParseChart("(120){4}" + string.Concat(System.Linq.Enumerable.Repeat("1,,", 500)));
        var results = new SimaiTimingPoint[16][];

        System.Threading.Tasks.Parallel.For(0, results.Length, i => results[i] = chart.CommaTimings.ToArray());

        Assert.Equal(1001, results[0].Length);
        Assert.All(results, r => Assert.Same(results[0][500], r[500]));
        Assert.Same(chart.CommaTimings[0], chart.CommaTimings[0]);
        Assert.Equal(0.5, chart.CommaTimings[1].Timing, 12);
    }

    /// <summary>
    /// Empty chart: has nothing.
    /// </summary>
    [Fact]
    public void EmptyChart_HasNothing()
    {
        Assert.True(SimaiChart.Empty.IsEmpty);
        Assert.Equal(string.Empty, SimaiChart.Empty.Level);
        Assert.Equal(string.Empty, SimaiChart.Empty.Designer);
        Assert.Equal(string.Empty, SimaiChart.Empty.Fumen);
        Assert.True(SimaiChart.Empty.CommaTimings.IsEmpty);
    }

    /// <summary>
    /// File: fills missing charts and keeps seven.
    /// </summary>
    [Fact]
    public void File_FillsMissingChartsAndKeepsSeven()
    {
        var chart = new SimaiChart("1", "d", string.Empty, ReadOnlySpan<SimaiTimingPoint>.Empty);
        var charts = new[] { chart, chart, chart, chart, chart, chart, chart, chart };
        var full = new SimaiFile("t", "a", 1, "h", charts, new[] { new SimaiCommand("x", "y") }, "f");
        var partial = new SimaiFile(null!, null!, 0, null!, new[] { chart }, null, null!);

        Assert.Equal(7, full.Charts.Length);
        Assert.All(full.Charts, c => Assert.Same(chart, c));
        Assert.Equal(("t", "a", 1f, "h", "f"), (full.Title, full.Artist, full.Offset, full.Hash, full.FinalDesigner));
        Assert.Single(full.Commands);
        Assert.Same(chart, partial.Charts[0]);
        Assert.Same(SimaiChart.Empty, partial.Charts[6]);
        Assert.Equal((string.Empty, string.Empty, string.Empty, string.Empty), (partial.Title, partial.Artist, partial.Hash, partial.FinalDesigner));
        Assert.Empty(partial.Commands);
    }

    /// <summary>
    /// Empty file: has empty charts.
    /// </summary>
    [Fact]
    public void EmptyFile_HasEmptyCharts()
    {
        var file = SimaiFile.Empty("t", "a");

        Assert.All(file.Charts, c => Assert.Same(SimaiChart.Empty, c));
        Assert.Equal("t", file.Title);
        file.Title = "x";
        file.Artist = "y";
        file.FinalDesigner = "z";
        file.Offset = 2;
        file.Hash = "h";
        Assert.Equal(("x", "y", "z", 2f, "h"), (file.Title, file.Artist, file.FinalDesigner, file.Offset, file.Hash));
    }

    /// <summary>
    /// Metadata: enumerable constructor takes seven.
    /// </summary>
    [Fact]
    public void Metadata_EnumerableConstructorTakesSeven()
    {
        var values = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
        var metadata = new SimaiMetadata("t", "a", 1, values, values, values, new List<SimaiCommand> { new("x", "y") }, "h", "f");

        Assert.Equal(7, metadata.Designers.Length);
        Assert.Equal("7", metadata.Levels[6]);
        Assert.Equal("7", metadata.Fumens[6]);
        Assert.Single(metadata.Commands.ToArray());
        Assert.Equal(("t", "a", 1f, "h", "f"), (metadata.Title, metadata.Artist, metadata.Offset, metadata.Hash, metadata.FinalDesigner));
        var empty = new SimaiMetadata("t", "a", 0, (IEnumerable<string>?)null, null, null, null, "h");
        Assert.Null(empty.Designers[0]);
        Assert.True(empty.Commands.IsEmpty);
    }

    /// <summary>
    /// Metadata: enumerable constructor rejects missing hash.
    /// </summary>
    [Fact]
    public void Metadata_EnumerableConstructorRejectsMissingHash()
    {
        Assert.Throws<ArgumentNullException>(() => new SimaiMetadata("t", "a", 0, (IEnumerable<string>?)null, null, null, null, null!));
        Assert.Throws<ArgumentException>(() => new SimaiMetadata("t", "a", 0, (IEnumerable<string>?)null, null, null, null, string.Empty));
    }

    /// <summary>
    /// Metadata: span constructor copies and takes seven.
    /// </summary>
    [Fact]
    public void Metadata_SpanConstructorCopiesAndTakesSeven()
    {
        var values = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
        var commands = new[] { new SimaiCommand("x", "y") };
        var metadata = new SimaiMetadata("t", "a", 1, values.AsSpan(), values.AsSpan(0, 2), ReadOnlySpan<string>.Empty, commands.AsSpan(), string.Empty);
        values[0] = "changed";

        Assert.Equal("1", metadata.Designers[0]);
        Assert.Equal(7, metadata.Designers.Length);
        Assert.Equal("2", metadata.Levels[1]);
        Assert.Null(metadata.Levels[2]);
        Assert.Null(metadata.Fumens[0]);
        Assert.Equal(commands[0], metadata.Commands[0]);
        Assert.Throws<ArgumentNullException>(() => new SimaiMetadata("t", "a", 0, ReadOnlySpan<string>.Empty, ReadOnlySpan<string>.Empty, ReadOnlySpan<string>.Empty, ReadOnlySpan<SimaiCommand>.Empty, null!));
    }

    /// <summary>
    /// Command: compares name and value.
    /// </summary>
    [Fact]
    public void Command_ComparesNameAndValue()
    {
        var command = new SimaiCommand("a", "b");

        Assert.Equal(new SimaiCommand("a", "b"), command);
        Assert.True(command == new SimaiCommand("a", "b"));
        Assert.True(command != new SimaiCommand("a", "c"));
        Assert.False(command.Equals((object)"a"));
        Assert.True(command.Equals((object)new SimaiCommand("a", "b")));
        Assert.Equal(new SimaiCommand("a", "b").GetHashCode(), command.GetHashCode());
        Assert.Equal(string.Empty, new SimaiCommand("a", null).Value);
        KeyValuePair<string, string> pair = command;
        Assert.Equal(new KeyValuePair<string, string>("a", "b"), pair);
    }

    /// <summary>
    /// Command: rejects invalid names.
    /// </summary>
    [Fact]
    public void Command_RejectsInvalidNames()
    {
        Assert.Throws<ArgumentNullException>(() => new SimaiCommand(null!, "b"));
        Assert.Throws<ArgumentException>(() => new SimaiCommand(string.Empty, "b"));
    }

    /// <summary>
    /// Command: try parse.
    /// </summary>
    /// <param name="text">A test case value.</param>
    /// <param name="expected">A test case value.</param>
    /// <param name="prefix">A test case value.</param>
    /// <param name="value">A test case value.</param>
    [Theory]
    [InlineData("&wholebpm = 150 ", true, "wholebpm", "150")]
    [InlineData("&a=", true, "a", "")]
    [InlineData("&=b", false, null, null)]
    [InlineData("=b", false, null, null)]
    [InlineData("&ab", false, null, null)]
    public void Command_TryParse(string text, bool expected, string? prefix, string? value)
    {
        Assert.Equal(expected, SimaiCommand.TryParse(text.AsSpan(), out var command));
        if (expected)
        {
            Assert.Equal(prefix, command.Prefix);
            Assert.Equal(value, command.Value);
        }
    }

    /// <summary>
    /// Exceptions: keep position.
    /// </summary>
    [Fact]
    public void Exceptions_KeepPosition()
    {
        var markup = new InvalidSimaiMarkupException(1, 2, "c");
        var syntax = new InvalidSimaiSyntaxException(3, 4, "d", "m");
        var syntaxDefault = new InvalidSimaiSyntaxException(5, 6, "e");

        Assert.Equal((1, 2, "c"), (markup.Line, markup.Column, markup.Content));
        Assert.Equal((3, 4, "d", "m"), (syntax.Line, syntax.Column, syntax.Content, syntax.Message));
        Assert.Equal((5, 6, "e"), (syntaxDefault.Line, syntaxDefault.Column, syntaxDefault.Content));
        Assert.IsAssignableFrom<FormatException>(syntax);
    }
}
