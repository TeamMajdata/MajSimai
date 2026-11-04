using System;
using System.IO;
using System.Linq;
using System.Text;
using MajSimai.Diagnostics;
using Xunit;

namespace MajSimai.Tests.Diagnostics;

/// <summary>
/// Tests <see cref="SimaiSyntaxChecker"/> and <see cref="SimaiDiagnostic"/>.
/// </summary>
public sealed class SyntaxCheckerTests
{
    /// <summary>
    /// Check chart: reports every problem instead of stopping at the first one.
    /// </summary>
    [Fact]
    public void CheckChart_ReportsEveryProblemInsteadOfStoppingAtTheFirstOne()
    {
        var diagnostics = SimaiSyntaxChecker.CheckChart("(120){4}1/b,\n1-2[8:1],12E,\n3-5[8:1]*,9,1h[8:1:2],1(120)2,(abc)\n4,".AsSpan());

        Assert.Equal(
            new[]
            {
                SimaiDiagnosticCodes.EmptyNote,
                SimaiDiagnosticCodes.InvalidSlideEnd,
                SimaiDiagnosticCodes.MisplacedTouchArea,
                SimaiDiagnosticCodes.EmptySameHeadPart,
                SimaiDiagnosticCodes.InvalidNoteShape,
                SimaiDiagnosticCodes.MalformedDuration,
                SimaiDiagnosticCodes.NoteCutByDeclaration,
                SimaiDiagnosticCodes.InvalidDeclaration,
            },
            diagnostics.Select(d => d.Code).OrderBy(c => Array.IndexOf(Order, c)).ToArray());
        Assert.All(diagnostics, d => Assert.Equal(SimaiDiagnosticSeverity.Error, d.Severity));
        var slide = diagnostics.Single(d => d.Code == SimaiDiagnosticCodes.InvalidSlideEnd);
        Assert.Equal((2, 1), (slide.Line, slide.Column));
        Assert.Equal("1-2[8:1]", slide.Content);
        Assert.Null(slide.ChartIndex);
    }

    /// <summary>
    /// Check chart: reports advice as warnings.
    /// </summary>
    [Fact]
    public void CheckChart_ReportsAdviceAsWarnings()
    {
        var diagnostics = SimaiSyntaxChecker.CheckChart("(120){4}1v1[8:1],1A3K3[8:1],1P0000K8[8:1],".AsSpan());

        Assert.Equal(3, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal(SimaiDiagnosticSeverity.Warning, d.Severity));
        Assert.Equal(
            new[] { SimaiDiagnosticCodes.InvalidSlideEnd, SimaiDiagnosticCodes.ZeroLengthSlideCode, SimaiDiagnosticCodes.ManyTurnsSlideCode },
            diagnostics.Select(d => d.Code));
        Assert.Contains("Majdata extension", diagnostics[0].Message);
        SimaiParser.ParseChart("(120){4}1v1[8:1],1A3K3[8:1],1P0000K8[8:1],".AsSpan(), SimaiParserOptions.Strict);
    }

    /// <summary>
    /// Check chart: reports nothing for a valid chart.
    /// </summary>
    [Fact]
    public void CheckChart_ReportsNothingForAValidChart()
    {
        Assert.Empty(SimaiSyntaxChecker.CheckChart("(120){4}1-3[8:1]*-5[8:1]/2h[4:1],B1/Cf,1A357K1[2:1],1`2,\nE".AsSpan()));
    }

    /// <summary>
    /// Check: reports file lines and chart indices.
    /// </summary>
    [Fact]
    public void Check_ReportsFileLinesAndChartIndices()
    {
        const string Content =
            "&title=Song\n" +
            "&title=Other\n" +
            "&broken\n" +
            "&first=x\n" +
            "&inote_2=\n" +
            "\n" +
            "(120){4}\n" +
            "  1,9,\n" +
            "&inote_3=(120)1-2[8:1],\n" +
            "1,2";

        var diagnostics = SimaiSyntaxChecker.Check(Content.AsSpan());

        var summary = diagnostics.Select(d => (d.Code, d.Line, d.ChartIndex, d.Severity)).ToArray();
        Assert.Equal(
            new[]
            {
                (SimaiDiagnosticCodes.DuplicateField, 2, (int?)null, SimaiDiagnosticSeverity.Warning),
                (SimaiDiagnosticCodes.FieldWithoutValue, 3, null, SimaiDiagnosticSeverity.Error),
                (SimaiDiagnosticCodes.InvalidOffset, 4, null, SimaiDiagnosticSeverity.Error),
                (SimaiDiagnosticCodes.InvalidNoteShape, 8, 2, SimaiDiagnosticSeverity.Error),
                (SimaiDiagnosticCodes.InvalidSlideEnd, 9, 3, SimaiDiagnosticSeverity.Error),
                (SimaiDiagnosticCodes.UnterminatedNote, 10, 3, SimaiDiagnosticSeverity.Error),
            },
            summary);
        Assert.Equal(3, diagnostics.Single(d => d.Code == SimaiDiagnosticCodes.InvalidNoteShape).Column);
    }

    /// <summary>
    /// Check: reads streams with their encoding.
    /// </summary>
    [Fact]
    public void Check_ReadsStreamsWithTheirEncoding()
    {
        var bytes = Encoding.Unicode.GetBytes("&inote_1=(120)9,");

        Assert.Single(SimaiSyntaxChecker.Check(new MemoryStream(bytes), Encoding.Unicode));
        Assert.Single(SimaiSyntaxChecker.Check(new MemoryStream(Encoding.UTF8.GetBytes("&inote_1=(120)9,"))));
        Assert.Throws<ArgumentNullException>(() => SimaiSyntaxChecker.Check(null!, null));
    }

    /// <summary>
    /// Errors: are exactly what the strict mode rejects.
    /// </summary>
    /// <param name="body">A test case value.</param>
    [Theory]
    [InlineData("1,2,3,")]
    [InlineData("1,2")]
    [InlineData("1v1[8:1],")]
    [InlineData("1-b5[8:1],")]
    [InlineData("&broken")]
    [InlineData("1,E,\n2,")]
    [InlineData("1-2[8:1],")]
    public void Errors_AreExactlyWhatTheStrictModeRejects(string body)
    {
        var content = body.StartsWith("&", StringComparison.Ordinal) ? body : "&inote_1=(120)" + body;
        var hasErrors = SimaiSyntaxChecker.Check(content.AsSpan()).Any(d => d.Severity == SimaiDiagnosticSeverity.Error);
        var strictFails = false;
        try
        {
            SimaiParser.Parse(content.AsSpan(), "h", SimaiParserOptions.Strict);
        }
        catch (InvalidSimaiMarkupException)
        {
            strictFails = true;
        }

        Assert.Equal(strictFails, hasErrors);
    }

    /// <summary>
    /// Diagnostic: keeps its values and formats them.
    /// </summary>
    [Fact]
    public void Diagnostic_KeepsItsValuesAndFormatsThem()
    {
        var diagnostic = new SimaiDiagnostic(SimaiDiagnosticSeverity.Error, "SM201", "message", 3, 4, "b", 5);
        var metadata = new SimaiDiagnostic(SimaiDiagnosticSeverity.Warning, "SM403", "twice", 1, 1, "title", null);

        Assert.Equal((SimaiDiagnosticSeverity.Error, "SM201", "message", 3, 4, "b", (int?)5),
                     (diagnostic.Severity, diagnostic.Code, diagnostic.Message, diagnostic.Line, diagnostic.Column, diagnostic.Content, diagnostic.ChartIndex));
        Assert.Equal("inote_5 3:4: Error SM201: message", diagnostic.ToString());
        Assert.Equal("1:1: Warning SM403: twice", metadata.ToString());
        Assert.Throws<ArgumentNullException>(() => new SimaiDiagnostic(SimaiDiagnosticSeverity.Error, null!, "m", 1, 1, "c", null));
        Assert.Throws<ArgumentNullException>(() => new SimaiDiagnostic(SimaiDiagnosticSeverity.Error, "c", null!, 1, 1, "c", null));
        Assert.Throws<ArgumentNullException>(() => new SimaiDiagnostic(SimaiDiagnosticSeverity.Error, "c", "m", 1, 1, null!, null));
    }

    /// <summary>
    /// The order of the codes in <see cref="CheckChart_ReportsEveryProblemInsteadOfStoppingAtTheFirstOne"/>.
    /// </summary>
    private static readonly string[] Order =
    {
        SimaiDiagnosticCodes.EmptyNote,
        SimaiDiagnosticCodes.InvalidSlideEnd,
        SimaiDiagnosticCodes.MisplacedTouchArea,
        SimaiDiagnosticCodes.EmptySameHeadPart,
        SimaiDiagnosticCodes.InvalidNoteShape,
        SimaiDiagnosticCodes.MalformedDuration,
        SimaiDiagnosticCodes.NoteCutByDeclaration,
        SimaiDiagnosticCodes.InvalidDeclaration,
    };
}
