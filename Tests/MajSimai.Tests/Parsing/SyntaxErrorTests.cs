using System;
using System.Linq;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests the malformed text that throws in every mode.
/// </summary>
public sealed class SyntaxErrorTests
{
    /// <summary>
    /// Unclosed declarations: throw at the opening character.
    /// </summary>
    /// <param name="fumen">A test case value.</param>
    /// <param name="kind">A test case value.</param>
    /// <param name="column">A test case value.</param>
    [Theory]
    [InlineData("1,(120", "BPM", 3)]
    [InlineData("1,{4\n2,", "beat", 3)]
    [InlineData("(120)<HS*2", "HS / SV", 6)]
    [InlineData("(", "BPM", 1)]
    public void UnclosedDeclarations_ThrowAtTheOpeningCharacter(string fumen, string kind, int column)
    {
        var error = Assert.Throws<InvalidSimaiSyntaxException>(() => SimaiParser.ParseChart(fumen));

        Assert.Contains($"The {kind} declaration", error.Message);
        Assert.Contains("is not closed", error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(column, error.Column);
    }

    /// <summary>
    /// Malformed notes: throw a note exception.
    /// </summary>
    /// <param name="note">A test case value.</param>
    /// <param name="problem">A test case value.</param>
    [Theory]
    [InlineData("1/b/2", "has no button or sensor")]
    [InlineData("2h]4:1[", "has ']' before '['")]
    [InlineData("Ch]4:1[", "has ']' before '['")]
    [InlineData("1h[8:1:2]", "too many ':' or '#'")]
    [InlineData("1h[1#2#3]", "too many ':' or '#'")]
    [InlineData("1-5[8:1:2]", "too many ':' or '#'")]
    [InlineData("1-5[1#2#3#4#5]", "too many ':' or '#'")]
    [InlineData("1-5[3#9#1.5]", "text between the '#'")]
    [InlineData("1-5[3#9#160#8:1]", "text between the '#'")]
    [InlineData("3-5[8:1]*", "has an empty part")]
    [InlineData("1-3[8:1]**-5[8:1]", "has an empty part")]
    [InlineData("12E", "touch area letter")]
    [InlineData("1-5E[8:1]", "touch area letter")]
    [InlineData("B1C", "touch area letter")]
    [InlineData("1A3-5[8:1]", "touch area letter")]
    public void MalformedNotes_ThrowANoteException(string note, string problem)
    {
        var error = Assert.Throws<InvalidSimaiNoteException>(() => Notes(note));

        Assert.Contains(problem, error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(ChartHelper.Prefix.Length + 1, error.Column);
        Assert.Contains(note, error.Message);
    }

    /// <summary>
    /// Malformed notes: also throw in strict mode.
    /// </summary>
    [Fact]
    public void MalformedNotes_AlsoThrowInStrictMode()
    {
        Assert.Throws<InvalidSimaiNoteException>(() => SimaiParser.ParseChart("(120){4}1/b,".AsSpan(), SimaiParserOptions.Strict));
    }

    /// <summary>
    /// Error: in a long chart is the first one in the text.
    /// </summary>
    [Fact]
    public void Error_InALongChartIsTheFirstOneInTheText()
    {
        var timings = Enumerable.Repeat("1", 2000).ToArray();
        timings[1500] = "2/x";
        timings[700] = "1/b";
        timings[1900] = "1h[1:2:3]";
        var fumen = "(120){4}" + string.Join(",\n", timings) + ",";

        for (var i = 0; i < 10; i++)
        {
            var error = Assert.Throws<InvalidSimaiNoteException>(() => SimaiParser.ParseChart(fumen));
            Assert.Equal(701, error.Line);
            Assert.Equal("b", error.Content);
        }
    }

    /// <summary>
    /// Failed chart: in a lenient file keeps its text.
    /// </summary>
    [Fact]
    public void FailedChart_InALenientFileKeepsItsText()
    {
        var file = SimaiParser.Parse("&inote_1=(120)1,\n&inote_2=(120)1/b,".AsSpan(), string.Empty);

        Assert.Single(file.Charts[0].NoteTimings.ToArray());
        Assert.True(file.Charts[1].IsEmpty);
        Assert.Equal("(120)1/b,", file.Charts[1].Fumen);
    }
}
