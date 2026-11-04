using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MajSimai.Diagnostics;
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
        output?.WriteLine($"Charts cut at the end marker E or with a final || for 2.x: {checker.EndMarkerCharts}");
        output?.WriteLine($"Timing points with Slide Code not compared note by note: {checker.SlideCodeTimings}");
        foreach (var failure in checker.IntentionalFailures.OrderByDescending(f => f.Value))
        {
            output?.WriteLine($"Intended differences ({failure.Key}): {failure.Value}");
        }
        output?.WriteLine($"Differences: {differences.Count}");
        foreach (var difference in differences.OrderBy(d => d, StringComparer.Ordinal).Take(50))
        {
            output?.WriteLine(difference);
        }
        Assert.Empty(differences);
    }

    /// <summary>
    /// The syntax checker reports an error for exactly the samples the strict mode rejects. The test also reports how
    /// often each diagnostic occurs.
    /// </summary>
    [Fact]
    public void Samples_SyntaxCheckerMatchesStrictMode()
    {
        var directory = Environment.GetEnvironmentVariable(SamplesDirectoryVariable);
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Skip($"Set {SamplesDirectoryVariable} to a folder of maidata.txt files to run this test.");
        }
        var files = Directory.GetFiles(directory!, "maidata.txt", SearchOption.AllDirectories);
        var codes = new ConcurrentDictionary<string, long>();
        var mismatches = new ConcurrentBag<string>();
        var filesWithErrors = 0;
        var filesWithWarnings = 0;
        Parallel.ForEach(files, file =>
        {
            var bytes = File.ReadAllBytes(file);
            using var stream = new MemoryStream(bytes, false);
            var diagnostics = SimaiSyntaxChecker.Check(stream);
            foreach (var diagnostic in diagnostics)
            {
                codes.AddOrUpdate($"{diagnostic.Code} {diagnostic.Severity}", 1, (_, count) => count + 1);
            }
            var hasErrors = diagnostics.Any(d => d.Severity == SimaiDiagnosticSeverity.Error);
            if (hasErrors)
            {
                Interlocked.Increment(ref filesWithErrors);
            }
            if (diagnostics.Any(d => d.Severity == SimaiDiagnosticSeverity.Warning))
            {
                Interlocked.Increment(ref filesWithWarnings);
            }
            string? strictError = null;
            try
            {
                SimaiParser.Parse(new MemoryStream(bytes, false), SimaiParserOptions.Strict);
            }
            catch (InvalidSimaiMarkupException e)
            {
                strictError = e.Message;
            }
            if (hasErrors != (strictError is not null))
            {
                mismatches.Add($"{file}: checker errors {hasErrors}, strict error {strictError}");
            }
        });
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Files: {files.Length}, with errors (rejected by strict mode): {filesWithErrors}, with warnings: {filesWithWarnings}");
        foreach (var code in codes.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            output?.WriteLine($"{code.Key}: {code.Value}");
        }
        foreach (var mismatch in mismatches.Take(20))
        {
            output?.WriteLine(mismatch);
        }
        Assert.Empty(mismatches);
    }
}
