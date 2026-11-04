using System;
using System.Threading.Tasks;
using Xunit;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests reading timing, declarations and errors of charts through <see cref="SimaiParser"/>.
/// </summary>
public sealed class ChartParsingTests
{
    /// <summary>
    /// Timing: follows bpm and beats.
    /// </summary>
    [Fact]
    public void Timing_FollowsBpmAndBeats()
    {
        var chart = SimaiParser.ParseChart("(120){4}1,2,{8}3,(60)4,");
        var timings = chart.NoteTimings;

        Assert.Equal(new[] { 0, 0.5, 1.0, 1.25 }, new[] { timings[0].Timing, timings[1].Timing, timings[2].Timing, timings[3].Timing });
        Assert.Equal(60f, timings[3].Bpm);
        Assert.Equal(5, chart.CommaTimings.Length);
        Assert.Equal(1.75, chart.CommaTimings[4].Timing, 12);
    }

    /// <summary>
    /// Beats in seconds: set the interval.
    /// </summary>
    [Fact]
    public void BeatsInSeconds_SetTheInterval()
    {
        var chart = SimaiParser.ParseChart("(120){#0.25}1,2,");

        Assert.Equal(0.25, chart.NoteTimings[1].Timing, 6);
    }

    /// <summary>
    /// H speed: applies to note timings.
    /// </summary>
    [Fact]
    public void HSpeed_AppliesToNoteTimings()
    {
        var chart = SimaiParser.ParseChart("(120)<HS*2.5>1,");

        Assert.Equal(2.5f, chart.NoteTimings[0].HSpeed);
    }

    /// <summary>
    /// Raw content: has no whitespace.
    /// </summary>
    [Fact]
    public void RawContent_HasNoWhitespace()
    {
        var chart = SimaiParser.ParseChart("(120)1 / 2\r\n/3,");

        Assert.Equal("1/2/3", chart.NoteTimings[0].RawContent);
        Assert.Equal(3, chart.NoteTimings[0].Notes.Length);
    }

    /// <summary>
    /// Positions: point at the comma.
    /// </summary>
    [Fact]
    public void Positions_PointAtTheComma()
    {
        var chart = SimaiParser.ParseChart("(120)\n12,");
        var timing = chart.NoteTimings[0];

        Assert.Equal(8, timing.RawTextPosition);
        Assert.Equal(2, timing.RawTextPositionY);
        Assert.Equal(3, timing.RawTextPositionX);
    }

    /// <summary>
    /// Level and designer: are kept.
    /// </summary>
    [Fact]
    public void LevelAndDesigner_AreKept()
    {
        var fumen = "(120)1,";
        var chart = SimaiParser.ParseChart("13+", "someone", fumen.AsSpan());

        Assert.Equal("13+", chart.Level);
        Assert.Equal("someone", chart.Designer);
        Assert.Equal(fumen, chart.Fumen);
        Assert.False(chart.IsEmpty);
    }

    /// <summary>
    /// Empty text: gives empty chart.
    /// </summary>
    [Fact]
    public void EmptyText_GivesEmptyChart()
    {
        var chart = SimaiParser.ParseChart(ReadOnlySpan<char>.Empty);

        Assert.True(chart.IsEmpty);
        Assert.Equal(string.Empty, chart.Fumen);
        Assert.True(chart.CommaTimings.IsEmpty);
    }

    /// <summary>
    /// Null level: is rejected.
    /// </summary>
    [Fact]
    public void NullLevel_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => SimaiParser.ParseChart(null!, string.Empty, "1,".AsSpan()));
        Assert.Throws<ArgumentNullException>(() => SimaiParser.ParseChart(string.Empty, null!, "1,".AsSpan()));
    }

    /// <summary>
    /// Malformed declarations: throw.
    /// </summary>
    /// <param name="fumen">A test case value.</param>
    /// <param name="exceptionType">A test case value.</param>
    /// <param name="message">A test case value.</param>
    [Theory]
    [InlineData("(abc)1,", typeof(InvalidSimaiSyntaxException), "BPM value must be a number")]
    [InlineData("{}1,", typeof(InvalidSimaiSyntaxException), "Beats value must be a number")]
    [InlineData("{x}1,", typeof(InvalidSimaiSyntaxException), "Beats value must be a number")]
    [InlineData("{#x}1,", typeof(InvalidSimaiSyntaxException), "Beats value must be a number")]
    [InlineData("<HS*x>1,", typeof(InvalidSimaiMarkupException), "HSpeed value must be a number")]
    [InlineData("<SV*x>1,", typeof(InvalidSimaiMarkupException), "SVeloc value must be a number")]
    [InlineData("<HS>1,", typeof(InvalidSimaiSyntaxException), "Unexpected HS / SV declaration syntax")]
    [InlineData("<HS*1*2>1,", typeof(InvalidSimaiSyntaxException), "Unexpected HS declaration syntax")]
    [InlineData("<XX*1>1,", typeof(InvalidSimaiSyntaxException), "Unexpected HS / SV declaration syntax \"XX\", is it \"HS\" or \"SV\"?")]
    [InlineData("|x", typeof(InvalidSimaiMarkupException), "Unexpected character \"|\"")]
    [InlineData("1,(", typeof(InvalidSimaiMarkupException), "Unexpected character \"(\"")]
    [InlineData("1,{", typeof(InvalidSimaiMarkupException), "Unexpected character \"{\"")]
    [InlineData("<1>", typeof(InvalidSimaiMarkupException), "Unexpected character \"<\"")]
    public void MalformedDeclarations_Throw(string fumen, Type exceptionType, string message)
    {
        var error = Assert.Throws(exceptionType, () => SimaiParser.ParseChart(fumen));

        Assert.Equal(message, error.Message);
        Assert.True(((InvalidSimaiMarkupException)error).Line >= 1);
    }

    /// <summary>
    /// Exception: reports position and content.
    /// </summary>
    [Fact]
    public void Exception_ReportsPositionAndContent()
    {
        var error = Assert.Throws<InvalidSimaiSyntaxException>(() => SimaiParser.ParseChart("1,\n( x )"));

        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
        Assert.Equal("x", error.Content);
    }

    /// <summary>
    /// Parse chart async: matches sync parse.
    /// </summary>
    [Fact]
    public async Task ParseChartAsync_MatchesSyncParse()
    {
        const string Fumen = "(120){4}1-5[8:1]/2,3,";
        var expected = SimaiParser.ParseChart("1", "d", Fumen.AsSpan());
        var charts = new[]
        {
            await SimaiParser.ParseChartAsync(Fumen),
            await SimaiParser.ParseChartAsync(Fumen.AsMemory()),
            await SimaiParser.ParseChartAsync("1", "d", Fumen.AsMemory()),
            await SimaiParser.ParseChartAsync("1", "d", Fumen),
        };

        foreach (var chart in charts)
        {
            Assert.Null(ModelComparer.FindDifference(expected.NoteTimings.ToArray(), chart.NoteTimings.ToArray()));
            Assert.Equal(Fumen, chart.Fumen);
        }
        Assert.Equal("1", charts[3].Level);
        Assert.Equal("d", charts[3].Designer);
    }
}
