using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Compares the current parser with MajSimai 2.2.3 on generated charts and files.
/// </summary>
public sealed class FuzzEquivalenceTests
{
    /// <summary>
    /// The seeds of the fuzz runs. Each seed generates a different set of charts.
    /// </summary>
    public static TheoryData<int> Seeds => new() { 1, 2, 3, 4, 5, 6, 7, 8 };

    /// <summary>
    /// Generated charts parse to the same result in both parsers.
    /// </summary>
    /// <param name="seed">The fuzzer seed.</param>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GeneratedCharts_MatchLegacyParser(int seed)
    {
        var fuzzer = new ChartFuzzer(seed);
        var checker = new EquivalenceChecker();
        var differences = new List<string>();
        for (var i = 0; i < 1500; i++)
        {
            var chart = fuzzer.NextChart(i % 3 == 0 ? 300 : 40);
            var difference = checker.CheckChart(chart);
            if (difference is not null)
            {
                differences.Add($"chart {i}: {difference}\n{chart}");
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Compared notes: {checker.ComparedNotes}, charts that threw: {checker.ChartErrors}, " +
            $"normalized: {checker.Normalizer.HeadlessSlideHeadFlags} + {checker.Normalizer.NonSlideNoHeadFlags}");
        Assert.True(differences.Count == 0, string.Join("\n\n", differences.Take(5)));
    }

    /// <summary>
    /// Generated files with charts and fields parse to the same result in both parsers.
    /// </summary>
    /// <param name="seed">The fuzzer seed.</param>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GeneratedFiles_MatchLegacyParser(int seed)
    {
        var fuzzer = new ChartFuzzer(seed + 1000);
        var checker = new EquivalenceChecker();
        var differences = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            var file = new StringBuilder();
            file.Append("&title=Song ").Append(i).Append('\n');
            file.Append("  &artist =Someone\n&des=\n&first=").Append(i % 4 == 0 ? "x" : "0.5").Append('\n');
            file.Append("&lv_1=1\n&lv_1=2\n&des_3=a\n&des_5=b\n&wholebpm=120\n&=ignored\n&inote_8=x\n");
            for (var level = 1; level <= 7; level += 1 + (i % 3))
            {
                file.Append("&inote_").Append(level).Append('=').Append(fuzzer.NextChart(30)).Append("\n\n  ");
            }
            if (i % 10 == 0)
            {
                file.Append("\n&broken line\n");
            }
            var difference = checker.CheckFile(Encoding.UTF8.GetBytes(file.ToString()));
            if (difference is not null)
            {
                differences.Add($"file {i}: {difference}");
            }
        }

        Assert.True(differences.Count == 0, string.Join("\n\n", differences.Take(5)));
    }
}
