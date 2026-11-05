using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests <see cref="SimaiParserOptions"/> and the strict mode: text the lenient mode tolerates throws.
/// </summary>
public sealed class StrictModeTests
{
    /// <summary>
    /// Undefined text: is tolerated by the lenient mode and rejected by the strict mode.
    /// </summary>
    /// <param name="body">A test case value.</param>
    /// <param name="exceptionType">A test case value.</param>
    /// <param name="problem">A test case value.</param>
    [Theory]
    [InlineData("1(120)2,", typeof(InvalidSimaiSyntaxException), "is cut off by a declaration")]
    [InlineData("1,2", typeof(InvalidSimaiSyntaxException), "has no comma after it")]
    [InlineData("1,)2,", typeof(InvalidSimaiMarkupException), "is not part of a note")]
    [InlineData("||s3/4/5\n1,", typeof(InvalidSimaiSyntaxException), "must be two integers")]
    [InlineData("||sx/4\n1,", typeof(InvalidSimaiSyntaxException), "must be two integers")]
    [InlineData("1,E,\n2,", typeof(InvalidSimaiSyntaxException), "after the end marker")]
    [InlineData("1``2,", typeof(InvalidSimaiNoteException), "empty part between '`'")]
    [InlineData("1` `2,", typeof(InvalidSimaiNoteException), "empty part between '`'")]
    [InlineData("1//2,", typeof(InvalidSimaiNoteException), "empty note between '/'")]
    [InlineData("1-5,", typeof(InvalidSimaiNoteException), "Cannot read the note")]
    [InlineData("1-5*-3[8:1],", typeof(InvalidSimaiNoteException), "Cannot read the slide")]
    [InlineData("1h[a:b],", typeof(InvalidSimaiNoteException), "Cannot read the length")]
    [InlineData("1-b5[8:1],", typeof(InvalidSimaiNoteException), "has no effect")]
    [InlineData("9,", typeof(InvalidSimaiNoteException), "not a valid Tap")]
    [InlineData("1f,", typeof(InvalidSimaiNoteException), "not a valid Tap")]
    [InlineData("51Q,", typeof(InvalidSimaiNoteException), "not a valid Tap")]
    [InlineData("1hf,", typeof(InvalidSimaiNoteException), "not a valid Hold")]
    [InlineData("B9,", typeof(InvalidSimaiNoteException), "not a valid Touch")]
    [InlineData("C3,", typeof(InvalidSimaiNoteException), "not a valid Touch")]
    [InlineData("Cff,", typeof(InvalidSimaiNoteException), "not a valid Touch")]
    [InlineData("B1h[4:1]f[4:1],", typeof(InvalidSimaiNoteException), "not a valid TouchHold")]
    [InlineData("1-2[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for '-'")]
    [InlineData("1^5[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for '^'")]
    [InlineData("1v5[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 'v'")]
    [InlineData("1s4[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 's'")]
    [InlineData("1z6[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 'z'")]
    [InlineData("1w4[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 'w'")]
    [InlineData("1V44[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 'V'")]
    [InlineData("1V34[8:1],", typeof(InvalidSimaiNoteException), "its end button is not allowed for 'V'")]
    [InlineData("1V9[8:1],", typeof(InvalidSimaiNoteException), "needs a turn button")]
    [InlineData("1-[8:1],", typeof(InvalidSimaiNoteException), "needs an end button")]
    [InlineData("1-35[8:1],", typeof(InvalidSimaiNoteException), "where a shape is expected")]
    [InlineData("1-3[8:1]-5,", typeof(InvalidSimaiNoteException), "or only after the last one")]
    [InlineData("1-3[8:1]-5[8:1]-7,", typeof(InvalidSimaiNoteException), "or only after the last one")]
    [InlineData("1-3[8:1]-5-7[8:1],", typeof(InvalidSimaiNoteException), "or only after the last one")]
    [InlineData("1w5-1[8:1],", typeof(InvalidSimaiNoteException), "cannot be part of a connected slide")]
    [InlineData("1C3K5[8:1],", typeof(InvalidSimaiNoteException), "'C' takes no parameter")]
    [InlineData("1A9K5[8:1],", typeof(InvalidSimaiNoteException), "takes a button from 1 to 8")]
    [InlineData("1A3K9[8:1],", typeof(InvalidSimaiNoteException), "takes a button from 1 to 8")]
    [InlineData("1AB3K5[8:1],", typeof(InvalidSimaiNoteException), "without a parameter")]
    [InlineData("1A3K56[8:1],", typeof(InvalidSimaiNoteException), "'K' must be the last command")]
    [InlineData("1A3#K5[8:1],", typeof(InvalidSimaiNoteException), "inside the Slide Code")]
    [InlineData("1P3Q3K6[8:1],", typeof(InvalidSimaiNoteException), "'P' and 'Q' need a node between them")]
    [InlineData("1P09K5[8:1],", typeof(InvalidSimaiNoteException), "circles 0 and 9 cannot follow each other")]
    [InlineData("1B2P3K4[8:1],", typeof(InvalidSimaiNoteException), "the node is inside the circle")]
    [InlineData("1CP0K4[8:1],", typeof(InvalidSimaiNoteException), "the node is inside the circle")]
    [InlineData("1B1P9K4[8:1],", typeof(InvalidSimaiNoteException), "the node is inside the circle")]
    [InlineData("1P3B2K4[8:1],", typeof(InvalidSimaiNoteException), "the node is inside the circle")]
    public void UndefinedText_IsToleratedLenientlyAndRejectedStrictly(string body, Type exceptionType, string problem)
    {
        var fumen = ChartHelper.Prefix + body;
        SimaiParser.ParseChart(fumen.AsSpan());

        var error = Assert.Throws(exceptionType, () => SimaiParser.ParseChart(fumen.AsSpan(), SimaiParserOptions.Strict));

        Assert.Contains(problem, error.Message);
    }

    /// <summary>
    /// Valid text: parses in strict mode.
    /// </summary>
    /// <param name="body">A test case value.</param>
    [Theory]
    [InlineData("1,2b,3x,4$,5$$,6@-2[8:1],7bx,8h,1h[4:1],2hb[150#8:3],3h[#1.5],")]
    [InlineData("B1,C,C1,C2,E8f,Ch,Chf[1:2],Cfh[1:2],E1h[4:3],A8hf,")]
    [InlineData("1-3[8:1],1-7[8:1],1^2[8:1],1^8[8:1],1v2[8:1],1v8[8:1],1>1[8:1],1<5[8:1],1p1[8:1],1qq8[8:1],1pp1[8:1],")]
    [InlineData("1s5[8:1],1z5[8:1],1w5[8:1],1V35[8:1],1V36[8:1],1V38[8:1],1V75[8:1],1V72[8:1],1V74[8:1],")]
    [InlineData("1-4q7-2[1:2],1-4[2:1]q7[2:1]-2[1:1],1-3[8:1]*-5[8:1]*>7[160#2],1?-5[8:1],1!-5[3##1.5],")]
    [InlineData("1-5b[8:1],1-5[8:1]b,1-5[8:1]m,1bx-5[8:1],1c-5[8:1],1/2/3,1`2`3,18,")]
    [InlineData("1A357K1[2:1],1bxA357K1b[2:1],1P3K6[8:1],1Q0K4[8:1],5P98CQ49K1[8:1],1Q2CP6K5[8:1],")]
    [InlineData("4Q7K5[8:1]*P2K3[8:1],8B6CB3K1[8:1],1^2^3Q9K4[1:1],1Q9K3-7[1:1],8Q79K3[1:1],1Q892K8[1:1],1K5[8:1],")]
    [InlineData("<HS*2><SV*0.5>||s3/4\n1,{#0.25}2,||comment\n3,\nE\n||after the end\n")]
    public void ValidText_ParsesInStrictMode(string body)
    {
        var chart = SimaiParser.ParseChart((ChartHelper.Prefix + body).AsSpan(), SimaiParserOptions.Strict);

        Assert.False(chart.IsEmpty);
    }

    /// <summary>
    /// Strict file: throws for the first failing chart.
    /// </summary>
    [Fact]
    public void StrictFile_ThrowsForTheFirstFailingChart()
    {
        const string Content = "&inote_1=(120)1,\n&inote_3=(120)1,2\n&inote_5=(120)9,";

        var error = Assert.Throws<InvalidSimaiSyntaxException>(() => SimaiParser.Parse(Content.AsSpan(), "h", SimaiParserOptions.Strict));
        Assert.Contains("has no comma after it", error.Message);
        var lenient = SimaiParser.Parse(Content.AsSpan(), "h");
        Assert.Equal(1, lenient.Charts[2].NoteTimings.Length);
    }

    /// <summary>
    /// Strict async file: throws for the first failing chart.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task StrictAsyncFile_ThrowsForTheFirstFailingChart()
    {
        const string Content = "&inote_2=(120)9,\n&inote_4=(120)1,2";
        var bytes = Encoding.UTF8.GetBytes(Content);

        var error = await Assert.ThrowsAsync<InvalidSimaiNoteException>(() => SimaiParser.ParseAsync(new MemoryStream(bytes), SimaiParserOptions.Strict));
        Assert.Contains("not a valid Tap", error.Message);
        await Assert.ThrowsAsync<InvalidSimaiNoteException>(() => SimaiParser.ParseAsync(Content, "h", SimaiParserOptions.Strict));
        await Assert.ThrowsAsync<InvalidSimaiNoteException>(() => SimaiParser.ParseChartAsync("(120)9,", SimaiParserOptions.Strict));
        var file = await SimaiParser.ParseAsync(Content, "h");
        Assert.Equal(9, file.Charts[1].NoteTimings[0].Notes[0].StartPosition);
    }

    /// <summary>
    /// Strict metadata: throws for a field without a value and an unreadable offset.
    /// </summary>
    [Fact]
    public void StrictMetadata_ThrowsForAFieldWithoutValueAndAnUnreadableOffset()
    {
        var missing = Assert.Throws<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadata("&title=a\n&broken".AsSpan(), "h", SimaiParserOptions.Strict));
        Assert.Equal(2, missing.Line);
        Assert.Equal("&broken", missing.Content);
        Assert.Throws<InvalidSimaiSyntaxException>(() => SimaiParser.ParseMetadata("&first=0,071".AsSpan(), "h", SimaiParserOptions.Strict));

        var lenient = SimaiParser.ParseMetadata("&title=a\n&broken\n&first=0,071".AsSpan(), "h");
        Assert.Equal("a", lenient.Title);
        Assert.Equal(0f, lenient.Offset);
        Assert.Equal(0f, SimaiParser.ParseMetadata("&first=".AsSpan(), "h", SimaiParserOptions.Strict).Offset);
    }

    /// <summary>
    /// Options: have lenient defaults and validate the tolerance.
    /// </summary>
    [Fact]
    public void Options_HaveLenientDefaultsAndValidateTheTolerance()
    {
        Assert.Equal(SimaiSyntaxTolerance.Lenient, new SimaiParserOptions().Tolerance);
        Assert.False(SimaiParserOptions.Lenient.IsStrict);
        Assert.True(SimaiParserOptions.Strict.IsStrict);
        Assert.Equal(SimaiSyntaxTolerance.Strict, new SimaiParserOptions(SimaiSyntaxTolerance.Strict).Tolerance);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimaiParserOptions((SimaiSyntaxTolerance)5));
    }

    /// <summary>
    /// Every overload: passes the options on.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task EveryOverload_PassesTheOptionsOn()
    {
        const string Bad = "&inote_1=(120)9,\n&broken";
        var bytes = Encoding.UTF8.GetBytes(Bad);
        var strict = SimaiParserOptions.Strict;

        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.Parse(new MemoryStream(bytes), strict));
        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.Parse(new MemoryStream(bytes), Encoding.UTF8, strict));
        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadata(new MemoryStream(bytes), strict));
        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadata(new MemoryStream(bytes), Encoding.UTF8, strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseAsync(Bad.AsMemory(), "h", strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseAsync(new MemoryStream(bytes), Encoding.UTF8, strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadataAsync(Bad, "h", strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadataAsync(Bad.AsMemory(), "h", strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadataAsync(new MemoryStream(bytes), strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadataAsync(new MemoryStream(bytes), Encoding.UTF8, strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseAsync(SimaiParser.ParseMetadata(Bad.AsSpan(), "h"), strict));
        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.Parse(SimaiParser.ParseMetadata(Bad.AsSpan(), "h"), strict));
        Assert.ThrowsAny<InvalidSimaiMarkupException>(() => SimaiParser.ParseChart("lv", "des", "(120)9,".AsSpan(), strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseChartAsync("(120)9,".AsMemory(), strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseChartAsync("lv", "des", "(120)9,".AsMemory(), strict));
        await Assert.ThrowsAnyAsync<InvalidSimaiMarkupException>(() => SimaiParser.ParseChartAsync("lv", "des", "(120)9,", strict));
        Assert.Equal(9, SimaiParser.Parse(new MemoryStream(bytes)).Charts[0].NoteTimings[0].Notes[0].StartPosition);
    }

    /// <summary>
    /// Strict mode: reads valid charts like the lenient mode.
    /// </summary>
    [Fact]
    public void StrictMode_ReadsValidChartsLikeTheLenientMode()
    {
        const string Fumen = "(120){4}1-3[8:1]*-5[8:1]/2h[4:1],B1/C,1`2,E";
        var lenient = SimaiParser.ParseChart(Fumen.AsSpan());
        var strict = SimaiParser.ParseChart(Fumen.AsSpan(), SimaiParserOptions.Strict);

        Assert.Null(ModelComparer.FindDifference(lenient, strict));
        Assert.Equal(4, strict.NoteTimings.Length);
        Assert.Equal(5, strict.NoteTimings.ToArray().Sum(t => t.Notes.Length) - 3);
    }
}
