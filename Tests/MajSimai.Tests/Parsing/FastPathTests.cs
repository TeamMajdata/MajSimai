using System;
using System.Globalization;
using System.Linq;
using MajSimai.Parsing;
using MajSimai.Tests.Equivalence;
using MajSimai.Utils;
using Xunit;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests the shortcuts the parser takes for speed: each must give exactly what the general path gives.
/// </summary>
public sealed class FastPathTests
{
    /// <summary>
    /// A single button: is read by the fast path as the same note the general path reads, in both modes.
    /// </summary>
    /// <param name="strict">Whether to read in strict mode.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleButton_IsTheNoteTheGeneralPathReads(bool strict)
    {
        // "1!" goes through marker detection, which removes the '!' (it only matters on slides), and ends as a tap "1".
        for (var button = '1'; button <= '8'; button++)
        {
            var fast = Read(button.ToString(), strict);
            var general = Read(button + "!", strict);

            Assert.Equal(general, fast);
            var note = Assert.Single(fast);
            Assert.Equal(SimaiNoteType.Tap, note.Type);
            Assert.Equal(button - '0', note.StartPosition);
            Assert.Equal(ParsedNoteFlags.UsingSV, note.Flags);
            Assert.Equal(button.ToString(), note.RawContent);
        }
    }

    /// <summary>
    /// Each parts and two-digit pairs of buttons: are read like the same parts read one by one by the general path.
    /// </summary>
    /// <param name="content">The timing point text.</param>
    /// <param name="generalContent">The same notes, written so that the general path reads them.</param>
    [Theory]
    [InlineData("1/2", "1!/2!")]
    [InlineData("3/7/8", "3!/7!/8!")]
    [InlineData("18", "1!/8!")]
    [InlineData("4/1-5[8:1]", "4!/1-5[8:1]")]
    public void ButtonParts_AreReadLikeTheGeneralPathReadsThem(string content, string generalContent)
    {
        Assert.Equal(Read(generalContent, false), Read(content, false));
        Assert.Equal(Read(generalContent, true), Read(content, true));
    }

    /// <summary>
    /// A single digit that is not a button: still takes the general path, which keeps it in lenient mode and rejects
    /// it in strict mode.
    /// </summary>
    [Fact]
    public void DigitThatIsNotAButton_TakesTheGeneralPath()
    {
        var lenient = Assert.Single(Read("9", false));

        Assert.Equal(9, lenient.StartPosition);
        Assert.Throws<InvalidSimaiNoteException>(() => Read("9", true));
        Assert.Throws<InvalidSimaiNoteException>(() => Read("0", true));
    }

    /// <summary>
    /// TryParseInt: gives the same result as <see cref="int.TryParse(string, NumberStyles, IFormatProvider, out int)"/>
    /// with the invariant culture, on plain digits and on everything the shortcut leaves to it.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("8")]
    [InlineData("007")]
    [InlineData("123456789")]
    [InlineData("1234567890")]
    [InlineData("2147483647")]
    [InlineData("2147483648")]
    [InlineData("99999999999")]
    [InlineData("+5")]
    [InlineData("-3")]
    [InlineData(" 4 ")]
    [InlineData("1a")]
    [InlineData("")]
    [InlineData("٣")]
    [InlineData("５")]
    public void TryParseInt_MatchesIntTryParse(string text)
    {
        var expectedResult = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected);

        var result = DurationReader.TryParseInt(text.AsSpan(), out var value);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expected, value);
    }

    /// <summary>
    /// Comma times: follow the BPM and beats in effect after every change, the same as computing them at every comma.
    /// </summary>
    [Fact]
    public void CommaTimes_FollowEveryBpmAndBeatChange()
    {
        // {#0.5} sets the beats from the BPM in effect; the later BPM change keeps those beats.
        var chart = SimaiParser.ParseChart("(120){4}1,,{8}2,,(60)3,,{#0.5}4,,(240)5,,{16}6,||a,b\n7,".AsSpan());
        var changes = new (int CommaCount, float Bpm, float Beats)[]
        {
            (2, 120f, 4f), (2, 120f, 8f), (2, 60f, 8f), (2, 60f, 240f / (60f * 0.5f)), (2, 240f, 240f / (60f * 0.5f)), (2, 240f, 16f),
        };
        var expected = new double[13];
        var time = 0d;
        var index = 0;
        foreach (var (commaCount, bpm, beats) in changes)
        {
            for (var i = 0; i < commaCount; i++)
            {
                expected[index++] = time;
                time += 1d / (bpm / 60d) * 4d / beats;
            }
        }
        expected[index] = time;

        var commas = chart.CommaTimings.ToArray();

        // The commas in the comment make no timing point: 12 commas and the end of the text.
        Assert.Equal(13, commas.Length);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), commas.Select(c => BitConverter.DoubleToInt64Bits(c.Timing)));
    }

    /// <summary>
    /// ShouldParseChartsInParallel: only for at least two charts with text that add up to the threshold.
    /// </summary>
    [Fact]
    public void ShouldParseChartsInParallel_NeedsTwoChartsAndEnoughText()
    {
        var half = new string(',', SimaiParser.ParallelChartTextLength / 2);
        var all = new string(',', SimaiParser.ParallelChartTextLength);

        Assert.True(SimaiParser.ShouldParseChartsInParallel(new[] { half, null!, half, string.Empty }));
        Assert.False(SimaiParser.ShouldParseChartsInParallel(new[] { half, half.Substring(1) }));
        Assert.False(SimaiParser.ShouldParseChartsInParallel(new[] { all, null!, string.Empty }));
        Assert.False(SimaiParser.ShouldParseChartsInParallel(new string[7]));
    }

    /// <summary>
    /// A large file: parses its charts in parallel and gives the same charts as parsing each on one thread.
    /// </summary>
    [Fact]
    public void LargeFile_GivesTheSameChartsInParallel()
    {
        var fuzzer = new ChartFuzzer(31);
        var charts = new[] { fuzzer.NextChart(1500, 0), string.Empty, fuzzer.NextChart(1500, 0), fuzzer.NextChart(200, 0) };
        var content = "&title=Large\n" + string.Concat(charts.Select((chart, i) => $"&inote_{i + 1}={chart}\n"));
        // The charts as the file holds them, after the metadata reader has read them out of it.
        var metadata = SimaiParser.ParseMetadata(content.AsSpan(), "h");
        Assert.True(SimaiParser.ShouldParseChartsInParallel(metadata.Fumens));

        var file = SimaiParser.Parse(metadata);

        for (var i = 0; i < SimaiMetadata.ChartCount; i++)
        {
            var sequential = SimaiParser.ParseChart(metadata.Levels[i] ?? string.Empty, metadata.Designers[i] ?? string.Empty, metadata.Fumens[i] ?? string.Empty, null, false);
            Assert.Null(ModelComparer.FindDifference(sequential, file.Charts[i]));
        }
    }

    /// <summary>
    /// SimaiNoteBuffer: grows, copies exactly its notes, and lets go of them when cleared.
    /// </summary>
    [Fact]
    public void SimaiNoteBuffer_GrowsCopiesAndClears()
    {
        var buffer = new SimaiNoteBuffer();
        var notes = Enumerable.Range(0, 40).Select(i => new SimaiNote { StartPosition = i }).ToArray();

        Assert.Same(Array.Empty<SimaiNote>(), buffer.ToArray());
        foreach (var note in notes)
        {
            buffer.Add(note);
        }
        var copy = buffer.ToArray();
        buffer.Clear();

        Assert.Equal(notes, copy);
        Assert.Equal(0, buffer.Count);
        Assert.Same(Array.Empty<SimaiNote>(), buffer.ToArray());
        buffer.Add(notes[3]);
        Assert.Same(notes[3], Assert.Single(buffer.ToArray()));
    }

    /// <summary>
    /// PooledBuffer.Clear: on an empty buffer does nothing, and on a filled one lets go of the items.
    /// </summary>
    [Fact]
    public void PooledBufferClear_IsHarmlessWhenEmpty()
    {
        using var buffer = new PooledBuffer<string>();

        buffer.Clear();
        buffer.Add("a");
        buffer.Clear();
        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        buffer.Add("b");
        Assert.Equal(new[] { "b" }, buffer.ToArray());
    }

    /// <summary>
    /// Reads the notes of a timing point text.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="strict">Whether to read in strict mode.</param>
    /// <returns>The notes read.</returns>
    private static ParsedNote[] Read(string content, bool strict)
    {
        var reporter = SyntaxReporter.For(strict ? SimaiParserOptions.Strict : SimaiParserOptions.Lenient);
        using var output = new PooledBuffer<ParsedNote>();
        NoteReader.ReadTimingPoint(new NoteSource(1.0, 120, content.AsSpan(), 1, 1, reporter), output);

        return output.AsSpan().ToArray();
    }
}
