using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Compares the current parser with MajSimai 2.2.3 on a folder of real <c>maidata.txt</c> files.
/// </summary>
/// <remarks>
/// Set the environment variable <c>MAJSIMAI_SAMPLES_DIR</c> to the folder; every <c>maidata.txt</c> below it is checked.
/// The test is skipped when the variable is not set. <c>tools/download-samples.py</c> downloads the samples.
/// </remarks>
public sealed class SampleEquivalenceTests
{
    /// <summary>
    /// The environment variable that names the sample folder.
    /// </summary>
    public const string SamplesDirectoryVariable = "MAJSIMAI_SAMPLES_DIR";

    /// <summary>
    /// Every sample parses to the same result, metadata and deparsed text in both parsers.
    /// </summary>
    [Fact]
    public void Samples_MatchLegacyParser()
    {
        var directory = Environment.GetEnvironmentVariable(SamplesDirectoryVariable);
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Skip($"Set {SamplesDirectoryVariable} to a folder of maidata.txt files to run this test.");
        }
        var files = Directory.GetFiles(directory!, "maidata.txt", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var checker = new EquivalenceChecker();
        var differences = new ConcurrentBag<string>();
        var checkedCount = 0;
        Parallel.ForEach(files, file =>
        {
            var difference = checker.CheckFile(File.ReadAllBytes(file));
            if (difference is not null)
            {
                differences.Add($"{file}: {difference}");
            }
            Interlocked.Increment(ref checkedCount);
        });
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Files: {checkedCount}");
        output?.WriteLine($"Files that failed in both parsers: {checker.FailedFiles}");
        output?.WriteLine($"Charts that failed in both parsers: {checker.FailedCharts}");
        output?.WriteLine($"Normalized headless slide head flags: {checker.Normalizer.HeadlessSlideHeadFlags}");
        output?.WriteLine($"Normalized IsSlideNoHead on non-slides: {checker.Normalizer.NonSlideNoHeadFlags}");
        output?.WriteLine($"Differences: {differences.Count}");
        foreach (var difference in differences.OrderBy(d => d, StringComparer.Ordinal).Take(50))
        {
            output?.WriteLine(difference);
        }
        Assert.Empty(differences);
    }
}
