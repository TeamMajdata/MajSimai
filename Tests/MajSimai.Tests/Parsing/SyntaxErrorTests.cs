using System;
using System.Linq;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests malformed text: unclosed declarations throw in every mode; malformed notes are skipped in lenient mode and
/// throw in strict mode.
/// </summary>
public sealed class SyntaxErrorTests
{
    /// <summary>
    /// Unclosed declarations: throw at the opening character in every mode.
    /// </summary>
    /// <param name="fumen">A test case value.</param>
    /// <param name="kind">A test case value.</param>
    /// <param name="column">A test case value.</param>
    [Theory]
    [InlineData("1,(120", "BPM", 3)]
    [InlineData("1,{4\n2,", "beat", 3)]
    [InlineData("(120)<HS*2", "HS / SV", 6)]
    [InlineData("(", "BPM", 1)]
    public void UnclosedDeclarations_ThrowAtTheOpeningCharacterInEveryMode(string fumen, string kind, int column)
    {
        foreach (var options in new[] { SimaiParserOptions.Lenient, SimaiParserOptions.Strict })
        {
            var error = Assert.Throws<InvalidSimaiSyntaxException>(() => SimaiParser.ParseChart(fumen.AsSpan(), options));

            Assert.Contains($"The {kind} declaration", error.Message);
            Assert.Contains("is not closed", error.Message);
            Assert.Equal(1, error.Line);
            Assert.Equal(column, error.Column);
        }
    }

    /// <summary>
    /// Malformed notes: are skipped in lenient mode and throw a note exception in strict mode.
    /// </summary>
    /// <param name="timingPoint">A test case value.</param>
    /// <param name="problem">A test case value.</param>
    /// <param name="lenientNoteCount">A test case value.</param>
    [Theory]
    [InlineData("1/b/2", "has no button or sensor", 2)]
    [InlineData("2h]4:1[/3", "has ']' before '['", 1)]
    [InlineData("Ch]4:1[/3", "has ']' before '['", 1)]
    [InlineData("1h[8:1:2]/3", "too many ':' or '#'", 1)]
    [InlineData("1h[1#2#3]/3", "too many ':' or '#'", 1)]
    [InlineData("1-5[8:1:2]/3", "too many ':' or '#'", 1)]
    [InlineData("1-5[1#2#3#4#5]/3", "too many ':' or '#'", 1)]
    [InlineData("1-5[3#9#1.5]/3", "text between the '#'", 1)]
    [InlineData("1-5[3#9#160#8:1]/3", "text between the '#'", 1)]
    [InlineData("3-5[8:1]*", "has an empty part", 2)]
    [InlineData("1-3[8:1]**-5[8:1]", "has an empty part", 3)]
    [InlineData("3/*-5[8:1]", "has an empty first part", 1)]
    [InlineData("12E/3", "touch area letter", 1)]
    [InlineData("1-5E[8:1]/3", "touch area letter", 1)]
    [InlineData("B1C/3", "touch area letter", 1)]
    [InlineData("1A3-5[8:1]/3", "touch area letter", 1)]
    public void MalformedNotes_AreSkippedLenientlyAndThrowStrictly(string timingPoint, string problem, int lenientNoteCount)
    {
        Assert.Equal(lenientNoteCount, Notes(timingPoint).Length);

        var fumen = ChartHelper.Prefix + timingPoint + ",";
        var error = Assert.Throws<InvalidSimaiNoteException>(() => SimaiParser.ParseChart(fumen.AsSpan(), SimaiParserOptions.Strict));

        Assert.Contains(problem, error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(ChartHelper.Prefix.Length + 1, error.Column);
        Assert.Contains(timingPoint, error.Message);
    }

    /// <summary>
    /// Skipped notes: leave the rest of the same head group.
    /// </summary>
    [Fact]
    public void SkippedNotes_LeaveTheRestOfTheSameHeadGroup()
    {
        var notes = Notes("1-3[8:1]**-5[8:1]*");

        Assert.Equal(new[] { SimaiNoteType.Tap, SimaiNoteType.Slide, SimaiNoteType.Slide }, notes.Select(n => n.Type));
        Assert.Equal(new[] { notes[1], notes[2] }, notes[0].RelatedNotes);
        Assert.Equal("1-5[8:1]", notes[2].RawContent);
    }

    /// <summary>
    /// Error: in a long strict chart is the first one in the text.
    /// </summary>
    [Fact]
    public void Error_InALongStrictChartIsTheFirstOneInTheText()
    {
        var timings = Enumerable.Repeat("1", 2000).ToArray();
        timings[1500] = "2/x";
        timings[700] = "1/b";
        timings[1900] = "1h[1:2:3]";
        var fumen = "(120){4}" + string.Join(",\n", timings) + ",";

        for (var i = 0; i < 10; i++)
        {
            var error = Assert.Throws<InvalidSimaiNoteException>(() => SimaiParser.ParseChart(fumen.AsSpan(), SimaiParserOptions.Strict));
            Assert.Equal(701, error.Line);
            Assert.Equal("b", error.Content);
        }
        var lenient = SimaiParser.ParseChart(fumen.AsSpan());
        Assert.Equal(2000, lenient.NoteTimings.Length);
        Assert.Equal(1, lenient.NoteTimings[700].Notes.Length);
    }

    /// <summary>
    /// Failed chart: in a lenient file keeps its text.
    /// </summary>
    [Fact]
    public void FailedChart_InALenientFileKeepsItsText()
    {
        var file = SimaiParser.Parse("&inote_1=(120)1,\n&inote_2=(120)1,{4".AsSpan(), string.Empty);

        Assert.Single(file.Charts[0].NoteTimings.ToArray());
        Assert.True(file.Charts[1].IsEmpty);
        Assert.Equal("(120)1,{4", file.Charts[1].Fumen);
    }
}
