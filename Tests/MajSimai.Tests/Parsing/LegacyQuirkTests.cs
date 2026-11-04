using System;
using System.Buffers;
using System.Globalization;
using System.Linq;
using System.Threading;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests the MajSimai 2.x behaviors that 3.x keeps on purpose, so existing charts are read the same way.
/// </summary>
public sealed class LegacyQuirkTests
{
    /// <summary>
    /// Every chart used by the tests in this class is read the same way by MajSimai 2.2.3.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    [Theory]
    [InlineData("(120){4}1h[8:1:2],1-5[3##60#4:1#9],")]
    [InlineData("(120){4}1`1`1`1`1`1`1`1`1`1,1`1`1`1`1`1`1`1`1`1`1`1`1`1`1`1`1`1,")]
    [InlineData("(120){4}3-5[8:1]*,1/b/2,1/2h]4:1[/3,2]x[h/3,1/1-5/2,18,1(120)2,1,2")]
    [InlineData("(120){4}E,1-5b[8:1],1-5[8:1]b,1-b5[8:1],12E,")]
    [InlineData("(120)1,\n||x\n2,")]
    [InlineData("(120)<SV*0.5>||s3/4\n1,")]
    [InlineData("1,||")]
    public void Quirks_MatchLegacyParser(string fumen)
    {
        Assert.Null(new MajSimai.Tests.Equivalence.EquivalenceChecker().CheckChart(fumen));
    }

    /// <summary>
    /// Ratio: ignores text after second colon.
    /// </summary>
    [Fact]
    public void Ratio_IgnoresTextAfterSecondColon()
    {
        Assert.Equal(Notes("1h[8:1]")[0].HoldTime, Notes("1h[8:1:2]")[0].HoldTime);
    }

    /// <summary>
    /// Slide bracket: reads only four hash parts.
    /// </summary>
    [Fact]
    public void SlideBracket_ReadsOnlyFourHashParts()
    {
        // [3##60#4:1#9] keeps the parts 3, "", 60 and 4:1.
        Assert.Equal(Notes("1-5[3##60#4:1]")[1].SlideTime, Notes("1-5[3##60#4:1#9]")[1].SlideTime);
    }

    /// <summary>
    /// Fake each: capacity depends on the rented array.
    /// </summary>
    [Fact]
    public void FakeEach_CapacityDependsOnTheRentedArray()
    {
        static int ExpectedParts(int separators)
        {
            var rented = ArrayPool<Range>.Shared.Rent(separators + 1);
            var capacity = rented.Length - (separators + 1);
            ArrayPool<Range>.Shared.Return(rented);
            return Math.Min(capacity, separators + 1);
        }

        for (var separators = 1; separators <= 20; separators++)
        {
            var text = string.Join("`", Enumerable.Repeat("1", separators + 1));
            var chart = Chart(text + ",");

            Assert.Equal(ExpectedParts(separators), chart.NoteTimings.Length);
        }
    }

    /// <summary>
    /// Trailing star: adds a tap on the head button.
    /// </summary>
    [Fact]
    public void TrailingStar_AddsATapOnTheHeadButton()
    {
        var notes = Notes("3-5[8:1]*");

        Assert.Equal(new[] { SimaiNoteType.Tap, SimaiNoteType.Slide, SimaiNoteType.Tap }, notes.Select(n => n.Type));
        Assert.Equal(3, notes[2].StartPosition);
        Assert.Empty(notes[2].RelatedNotes);
    }

    /// <summary>
    /// Empty note after markers: drops the rest of the timing point.
    /// </summary>
    [Fact]
    public void EmptyNoteAfterMarkers_DropsTheRestOfTheTimingPoint()
    {
        var notes = Notes("1/b/2");

        Assert.Equal(1, Assert.Single(notes).StartPosition);
    }

    /// <summary>
    /// Reversed hold brackets: drop the rest of the timing point.
    /// </summary>
    [Fact]
    public void ReversedHoldBrackets_DropTheRestOfTheTimingPoint()
    {
        var notes = Notes("1/2h]4:1[/3");

        Assert.Equal(1, Assert.Single(notes).StartPosition);
    }

    /// <summary>
    /// Reversed brackets: on short hold are ignored.
    /// </summary>
    [Fact]
    public void ReversedBrackets_OnShortHoldAreIgnored()
    {
        Assert.Equal(2, Notes("2]x[h/3").Length);
    }

    /// <summary>
    /// Unreadable note: is skipped.
    /// </summary>
    [Fact]
    public void UnreadableNote_IsSkipped()
    {
        var notes = Notes("1/1-5/2");

        Assert.Equal(new[] { 1, 2 }, notes.Select(n => n.StartPosition));
    }

    /// <summary>
    /// Two digits: are two taps.
    /// </summary>
    [Fact]
    public void TwoDigits_AreTwoTaps()
    {
        var notes = Notes("18");

        Assert.Equal(new[] { 1, 8 }, notes.Select(n => n.StartPosition));
        Assert.All(notes, n => Assert.True(n.IsEach));
    }

    /// <summary>
    /// Declarations: drop the note text read so far.
    /// </summary>
    [Fact]
    public void Declarations_DropTheNoteTextReadSoFar()
    {
        var chart = Chart("1(120)2,");

        Assert.Equal("2", chart.NoteTimings[0].RawContent);
    }

    /// <summary>
    /// Unterminated last note: is dropped.
    /// </summary>
    [Fact]
    public void UnterminatedLastNote_IsDropped()
    {
        var chart = Chart("1,2");

        Assert.Equal(1, chart.NoteTimings.Length);
        Assert.Equal(2, chart.CommaTimings.Length);
    }

    /// <summary>
    /// Timing point without notes: is kept.
    /// </summary>
    [Fact]
    public void TimingPointWithoutNotes_IsKept()
    {
        var chart = Chart("E,");

        var timing = Assert.Single(chart.NoteTimings.ToArray());
        Assert.Equal("E", timing.RawContent);
        Assert.True(timing.IsEmpty);
    }

    /// <summary>
    /// Comment at the end: throws index error.
    /// </summary>
    [Fact]
    public void CommentAtTheEnd_ThrowsIndexError()
    {
        var error = Assert.Throws<Exception>(() => SimaiParser.ParseChart("1,||"));

        Assert.Equal("Error at 1,5\n" + new IndexOutOfRangeException().Message, error.Message);
    }

    /// <summary>
    /// Column: counts comments and declarations like2x.
    /// </summary>
    [Fact]
    public void Column_CountsCommentsAndDeclarationsLike2x()
    {
        var chart = SimaiParser.ParseChart("(120)1,\n||x\n2,");

        // The opening parenthesis is counted twice.
        Assert.Equal(7, chart.NoteTimings[0].RawTextPositionX);
        Assert.Equal(1, chart.NoteTimings[0].RawTextPositionY);
        Assert.Equal(2, chart.NoteTimings[1].RawTextPositionX);
        Assert.Equal(3, chart.NoteTimings[1].RawTextPositionY);
    }

    /// <summary>
    /// Numbers: use invariant culture.
    /// </summary>
    [Fact]
    public void Numbers_UseInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var chart = SimaiParser.ParseChart("(120.5){4}1,");

            Assert.Equal(120.5f, chart.NoteTimings[0].Bpm);
            Assert.Equal(0.75, Notes("1h[#0.75]")[0].HoldTime, 12);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// Parse: sets invariant default culture.
    /// </summary>
    [Fact]
    public void Parse_SetsInvariantDefaultCulture()
    {
        SimaiParser.ParseChart("1,");

        Assert.Same(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentCulture);
        Assert.Same(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentUICulture);
    }

    /// <summary>
    /// Break after slide mark: only counts before bracket or at the end.
    /// </summary>
    [Fact]
    public void BreakAfterSlideMark_OnlyCountsBeforeBracketOrAtTheEnd()
    {
        Assert.True(Notes("1-5b[8:1]")[1].IsBreak);
        Assert.True(Notes("1-5[8:1]b")[1].IsBreak);
        var ignored = Notes("1-b5[8:1]");
        Assert.False(ignored[0].IsBreak);
        Assert.False(ignored[1].IsBreak);
    }

    /// <summary>
    /// Touch area letter anywhere: makes a touch.
    /// </summary>
    [Fact]
    public void TouchAreaLetterAnywhere_MakesATouch()
    {
        var touch = Assert.Single(Notes("12E"));

        Assert.Equal(SimaiNoteType.Touch, touch.Type);
        Assert.Equal('1', touch.TouchArea);
        Assert.Equal(2, touch.StartPosition);
    }

    /// <summary>
    /// Note timings: use fixed speed and signature values.
    /// </summary>
    [Fact]
    public void NoteTimings_UseFixedSpeedAndSignatureValues()
    {
        var chart = SimaiParser.ParseChart("(120)<SV*0.5>||s3/4\n1,");
        var note = chart.NoteTimings[0];
        var comma = chart.CommaTimings[0];

        Assert.Equal(1f, note.SVeloc);
        Assert.Equal(0, note.SignatureNumerator);
        Assert.Equal(0, note.SignatureDenominator);
        Assert.Equal(0.5f, comma.SVeloc);
        Assert.Equal(1f, comma.HSpeed);
        Assert.Equal(3, comma.SignatureNumerator);
        Assert.Equal(4, comma.SignatureDenominator);
    }
}
