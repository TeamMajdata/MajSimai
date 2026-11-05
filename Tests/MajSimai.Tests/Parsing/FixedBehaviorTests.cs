using System;
using System.Globalization;
using System.Linq;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests the MajSimai 2.x bugs that 3.x fixes without throwing.
/// </summary>
public sealed class FixedBehaviorTests
{
    /// <summary>
    /// Columns: are the real columns of the commas.
    /// </summary>
    [Fact]
    public void Columns_AreTheRealColumnsOfTheCommas()
    {
        var chart = SimaiParser.ParseChart("(120)1,\n||x\n  2,\n<HS*2>{4}3,");

        Assert.Equal((7, 1), (chart.NoteTimings[0].RawTextPositionX, chart.NoteTimings[0].RawTextPositionY));
        Assert.Equal((4, 3), (chart.NoteTimings[1].RawTextPositionX, chart.NoteTimings[1].RawTextPositionY));
        Assert.Equal((11, 4), (chart.NoteTimings[2].RawTextPositionX, chart.NoteTimings[2].RawTextPositionY));
        Assert.Equal(6, chart.NoteTimings[0].RawTextPosition);
    }

    /// <summary>
    /// Comment: at the end of the chart is a comment.
    /// </summary>
    [Theory]
    [InlineData("1,||")]
    [InlineData("1,||s")]
    [InlineData("1,|| comment, with 2, commas")]
    public void Comment_AtTheEndOfTheChartIsAComment(string fumen)
    {
        var chart = SimaiParser.ParseChart(fumen);

        Assert.Single(chart.NoteTimings.ToArray());
        Assert.Equal(2, chart.CommaTimings.Length);
    }

    /// <summary>
    /// Note timing points: carry the slide velocity and signature in effect.
    /// </summary>
    [Fact]
    public void NoteTimingPoints_CarryTheSlideVelocityAndSignatureInEffect()
    {
        var chart = SimaiParser.ParseChart("(120)<SV*0.5>||s3/4\n1,<HS*2>,");
        var note = chart.NoteTimings[0];

        Assert.Equal(0.5f, note.SVeloc);
        Assert.Equal((3, 4), (note.SignatureNumerator, note.SignatureDenominator));
        Assert.Equal(1f, chart.CommaTimings[0].HSpeed);
        Assert.Equal(2f, chart.CommaTimings[1].HSpeed);
    }

    /// <summary>
    /// End marker: ends the chart.
    /// </summary>
    [Fact]
    public void EndMarker_EndsTheChart()
    {
        var chart = SimaiParser.ParseChart("(120)1,2,\nE,\n3,4,");

        Assert.Equal(2, chart.NoteTimings.Length);
        Assert.Equal(3, chart.CommaTimings.Length);
        var end = chart.CommaTimings[2];
        Assert.Equal(10, end.RawTextPosition);
        Assert.Equal((1, 2), (end.RawTextPositionX, end.RawTextPositionY));
        Assert.Equal(1.0, end.Timing, 12);
    }

    /// <summary>
    /// End marker: is not a sensor with a number, even across whitespace and line breaks.
    /// </summary>
    [Fact]
    public void EndMarker_IsNotASensorWithANumber()
    {
        var chart = SimaiParser.ParseChart("(120)E1,E 2,E\n3,");

        Assert.Equal(new[] { 'E', 'E', 'E' }, chart.NoteTimings.ToArray().Select(t => t.Notes[0].TouchArea));
    }

    /// <summary>
    /// Fake each: keeps every part.
    /// </summary>
    [Fact]
    public void FakeEach_KeepsEveryPart()
    {
        var text = string.Join("`", Enumerable.Repeat("1", 40));

        Assert.Equal(40, Chart(text + ",").NoteTimings.Length);
    }

    /// <summary>
    /// Thousands separators: are not numbers.
    /// </summary>
    [Fact]
    public void ThousandsSeparators_AreNotNumbers()
    {
        Assert.Equal(0f, SimaiParser.ParseMetadata("&first=0,071".AsSpan(), string.Empty).Offset);
        Assert.Equal(0.071f, SimaiParser.ParseMetadata("&first=0.071".AsSpan(), string.Empty).Offset);
    }

    /// <summary>
    /// Field names and values: are trimmed.
    /// </summary>
    [Fact]
    public void FieldNamesAndValues_AreTrimmed()
    {
        var metadata = SimaiParser.ParseMetadata("&title = Song \n&wholebpm =  150\n&inote_1 = 1,".AsSpan(), string.Empty);

        Assert.Equal("Song", metadata.Title);
        Assert.Equal(new SimaiCommand("wholebpm", "150"), Assert.Single(metadata.Commands.ToArray()));
        Assert.Equal("1,", metadata.Fumens[0]);
    }

    /// <summary>
    /// Comments: only have a meaning in chart text.
    /// </summary>
    [Fact]
    public void Comments_OnlyHaveAMeaningInChartText()
    {
        var metadata = SimaiParser.ParseMetadata("&title=a||b\n||&artist=x\n&inote_1=1,||c\n2,".AsSpan(), string.Empty);

        Assert.Equal("a||b", metadata.Title);
        Assert.Equal(string.Empty, metadata.Artist);
        Assert.Equal(2, SimaiParser.ParseChart(metadata.Fumens[0].AsSpan()).NoteTimings.Length);
    }

    /// <summary>
    /// Slide code: star head keeps only the start button.
    /// </summary>
    [Fact]
    public void SlideCode_StarHeadKeepsOnlyTheStartButton()
    {
        var notes = Notes("1bxA357K1b[2:1]");

        Assert.Equal(2, notes.Length);
        Assert.Equal("1", notes[0].RawContent);
        Assert.True(notes[0].IsStar);
        Assert.True(notes[0].IsBreak);
        Assert.True(notes[0].IsEx);
        Assert.Equal(SimaiNoteType.Slide, notes[1].Type);
        Assert.Equal("1A357K1[2:1]", notes[1].RawContent);
        Assert.True(notes[1].IsBreak);
    }

    /// <summary>
    /// Slide code: marker inside the path does not mark the head.
    /// </summary>
    [Fact]
    public void SlideCode_MarkerInsideThePathDoesNotMarkTheHead()
    {
        var notes = Notes("1A3bK5[8:1]");

        Assert.False(notes[0].IsBreak);
        Assert.False(notes[1].IsBreak);
    }

    /// <summary>
    /// Slide code: same head group continues from the head button.
    /// </summary>
    [Fact]
    public void SlideCode_SameHeadGroupContinuesFromTheHeadButton()
    {
        var notes = Notes("4Q7K5[8:1]*P2K3[8:1]");

        Assert.Equal(3, notes.Length);
        Assert.Equal("4", notes[0].RawContent);
        Assert.Equal("4P2K3[8:1]", notes[2].RawContent);
        Assert.Equal(new[] { notes[1], notes[2] }, notes[0].RelatedNotes);
        Assert.True(notes[0].IsStarDouble);
    }
}

/// <summary>
/// Tests that parsing leaves the process-wide culture alone. The MajSimai 2.2.3 oracle used by other tests changes it, so
/// these tests do not run in parallel with them.
/// </summary>
[Collection(nameof(CultureTests))]
[CollectionDefinition(nameof(CultureTests), DisableParallelization = true)]
public sealed class CultureTests
{
    /// <summary>
    /// Parse: does not change the default culture of the process.
    /// </summary>
    [Fact]
    public void Parse_DoesNotChangeTheDefaultCulture()
    {
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = null;
            SimaiParser.Parse("&inote_1=(120)1,".AsSpan(), string.Empty);
            SimaiParser.Deparse(SimaiFile.Empty("t", "a"));

            Assert.Null(CultureInfo.DefaultThreadCurrentCulture);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }
}
