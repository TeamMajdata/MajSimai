using System;
using System.Globalization;
using System.Linq;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests the MajSimai 2.x behaviors that 3.x keeps in lenient mode, so existing charts are read the same way.
/// </summary>
public sealed class LegacyQuirkTests
{
    /// <summary>
    /// Every chart used by the tests in this class is read the same way by MajSimai 2.2.3.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    [Theory]
    [InlineData("(120){4}18,1(120)2,1/1-5/2,1-b5[8:1],1-5b[8:1],1-5[8:1]b,1,2")]
    [InlineData("(120){4}1`2`3,1h,Ch,1h[a:b],1?-5[8:1]*-3[8:1],1x*-5[8:1],")]
    [InlineData("(120)<SV*0.5>||s3/4/5\n1,||sx/y\n2,")]
    [InlineData("(120){4}51Q,8Q,9,0,B9,1f,")]
    [InlineData("(1E+02)<HS*NaN>1,2,")]
    public void KeptBehaviors_MatchLegacyParser(string fumen)
    {
        Assert.Null(new Equivalence.EquivalenceChecker().CheckChart(fumen));
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
    /// Declarations: drop the note text read so far in lenient mode.
    /// </summary>
    [Fact]
    public void Declarations_DropTheNoteTextReadSoFar()
    {
        var chart = Chart("1(120)2,");

        Assert.Equal("2", chart.NoteTimings[0].RawContent);
    }

    /// <summary>
    /// Unterminated last note: is dropped in lenient mode.
    /// </summary>
    [Fact]
    public void UnterminatedLastNote_IsDropped()
    {
        var chart = Chart("1,2");

        Assert.Equal(1, chart.NoteTimings.Length);
        Assert.Equal(2, chart.CommaTimings.Length);
    }

    /// <summary>
    /// Unreadable note: is skipped in lenient mode.
    /// </summary>
    [Fact]
    public void UnreadableNote_IsSkipped()
    {
        var notes = Notes("1/1-5/2");

        Assert.Equal(new[] { 1, 2 }, notes.Select(n => n.StartPosition));
    }

    /// <summary>
    /// Unreadable first part: skips its same head group in lenient mode.
    /// </summary>
    [Fact]
    public void UnreadableFirstPart_SkipsItsSameHeadGroup()
    {
        var notes = Notes("1-5*-3[8:1]/2");

        Assert.Equal(2, Assert.Single(notes).StartPosition);
    }

    /// <summary>
    /// Unreadable hold length: becomes zero in lenient mode.
    /// </summary>
    [Fact]
    public void UnreadableHoldLength_BecomesZero()
    {
        var hold = Assert.Single(Notes("1h[a:b]"));

        Assert.Equal(SimaiNoteType.Hold, hold.Type);
        Assert.Equal(0, hold.HoldTime);
    }

    /// <summary>
    /// Short hold: has no length.
    /// </summary>
    [Fact]
    public void ShortHold_HasNoLength()
    {
        Assert.Equal(0, Assert.Single(Notes("1h")).HoldTime);
        Assert.Equal(0, Assert.Single(Notes("Ch")).HoldTime);
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
    /// Signature: reads two parts and falls back to four.
    /// </summary>
    [Fact]
    public void Signature_ReadsTwoPartsAndFallsBackToFour()
    {
        var chart = SimaiParser.ParseChart("(120)||s3/4/5\n1,||sx/y\n2,");

        Assert.Equal((3, 4), (chart.CommaTimings[0].SignatureNumerator, chart.CommaTimings[0].SignatureDenominator));
        Assert.Equal((4, 4), (chart.CommaTimings[1].SignatureNumerator, chart.CommaTimings[1].SignatureDenominator));
    }

    /// <summary>
    /// Fake each: parts are a 128th note apart.
    /// </summary>
    [Fact]
    public void FakeEach_PartsAreA128thNoteApart()
    {
        var chart = Chart("1`2`3,");

        Assert.Equal(1.875 / 120, chart.NoteTimings[1].Timing - chart.NoteTimings[0].Timing, 12);
        Assert.Equal(1.875 / 120 * 2, chart.NoteTimings[2].Timing - chart.NoteTimings[0].Timing, 12);
    }

    /// <summary>
    /// Ratio: uses the BPM of the bracket.
    /// </summary>
    [Fact]
    public void Ratio_UsesTheBpmOfTheBracket()
    {
        Assert.Equal(1.0, Notes("1h[60#4:1]")[0].HoldTime, 12);
    }

    /// <summary>
    /// Numbers: use the invariant culture and allow exponents and NaN.
    /// </summary>
    [Fact]
    public void Numbers_UseInvariantCultureAndAllowExponentsAndNaN()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var chart = SimaiParser.ParseChart("(120.5){4}1,(1E+02)<HS*NaN>2,");

            Assert.Equal(120.5f, chart.NoteTimings[0].Bpm);
            Assert.Equal(100f, chart.NoteTimings[1].Bpm);
            Assert.True(float.IsNaN(chart.NoteTimings[1].HSpeed));
            Assert.Equal(0.75, Notes("1h[#0.75]")[0].HoldTime, 12);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// Buttons and sensors: outside the range are read in lenient mode.
    /// </summary>
    [Fact]
    public void ButtonsAndSensorsOutsideTheRange_AreReadInLenientMode()
    {
        Assert.Equal(9, Assert.Single(Notes("9")).StartPosition);
        Assert.Equal(9, Assert.Single(Notes("B9")).StartPosition);
    }

    /// <summary>
    /// Button letters: without K are plain text.
    /// </summary>
    [Fact]
    public void ButtonLettersWithoutK_ArePlainText()
    {
        var tap = Assert.Single(Notes("51Q"));

        Assert.Equal(SimaiNoteType.Tap, tap.Type);
        Assert.Equal("51Q", tap.RawContent);
    }
}
