using System.Threading.Tasks;
using MajSimai.Parsing;
using MajSimai.Tests.Equivalence;
using Xunit;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests that reading notes in parallel gives the same result as reading them on one thread.
/// </summary>
public sealed class DeterminismTests
{
    /// <summary>
    /// Parallel and sequential parse: are identical.
    /// </summary>
    /// <param name="seed">A test case value.</param>
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void ParallelAndSequentialParse_AreIdentical(int seed)
    {
        var fuzzer = new ChartFuzzer(seed);
        var checkedCount = 0;
        for (var i = 0; i < 40; i++)
        {
            var fumen = fuzzer.NextChart(ChartParser.ParallelThreshold * 4, 0);
            SimaiChart sequential;
            try
            {
                sequential = SimaiParser.ParseChart("lv", "des", fumen, null, false);
            }
            catch (System.Exception)
            {
                continue;
            }
            var parallel = SimaiParser.ParseChart("lv", "des", fumen, null, true);
            Assert.True(parallel.NoteTimings.Length > ChartParser.ParallelThreshold);
            Assert.Null(ModelComparer.FindDifference(sequential, parallel));
            checkedCount++;
        }

        Assert.True(checkedCount > 20);
    }

    /// <summary>
    /// Concurrent parses: are identical.
    /// </summary>
    [Fact]
    public void ConcurrentParses_AreIdentical()
    {
        var fumen = new ChartFuzzer(21).NextChart(2000, 0);
        var expected = SimaiParser.ParseChart("lv", "des", fumen, null, false);
        var results = new SimaiChart[16];

        Parallel.For(0, results.Length, i => results[i] = SimaiParser.ParseChart("lv", "des", fumen, null, true));

        foreach (var result in results)
        {
            Assert.Null(ModelComparer.FindDifference(expected, result));
        }
    }

    /// <summary>
    /// Long charts: match legacy parser when parallel.
    /// </summary>
    [Fact]
    public void LongCharts_MatchLegacyParserWhenParallel()
    {
        var fuzzer = new ChartFuzzer(31);
        var checker = new EquivalenceChecker();
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(checker.CheckChart(fuzzer.NextChart(ChartParser.ParallelThreshold * 3, 0), allowParallel: true));
        }
    }
}
