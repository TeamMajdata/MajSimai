using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MajSimai.Tests.Legacy;
using Xunit;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Checks the hashes of the current parser against MajSimai 2.2.3 on a folder of real <c>maidata.txt</c> files.
/// </summary>
/// <remarks>
/// Set the environment variable <c>MAJSIMAI_SAMPLES_DIR</c> to the folder; every <c>maidata.txt</c> below it is checked.
/// The test is skipped when the variable is not set. Unlike <see cref="SampleEquivalenceTests"/>, the legacy parser reads
/// the raw bytes of each file, so its <c>Hash</c> can be compared as it is.
/// </remarks>
public sealed class SampleHashTests
{
    /// <summary>
    /// For every sample, <see cref="SimaiFile.Hash"/> and <see cref="SimaiMetadata.Hash"/> are the same as in 2.2.3,
    /// <c>Md5</c> is the same MD5 as <c>Hash</c> written in hex instead of Base64, <c>Sha256</c> is the SHA-256 of the
    /// raw bytes, and each chart is hashed from its text without white space.
    /// </summary>
    [Fact]
    public async Task Samples_HashesMatchLegacyParser()
    {
        var directory = Environment.GetEnvironmentVariable(SampleEquivalenceTests.SamplesDirectoryVariable);
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Skip($"Set {SampleEquivalenceTests.SamplesDirectoryVariable} to a folder of maidata.txt files to run this test.");
        }
        var files = Directory.GetFiles(directory!, "maidata.txt", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var legacy = LegacyParser.Instance;
        var differences = new ConcurrentBag<string>();
        var legacyComparisons = 0;
        var legacyFailures = 0;
        var currentFailures = 0;
        var charts = 0;
        await Parallel.ForEachAsync(files, async (path, _) =>
        {
            var bytes = File.ReadAllBytes(path);
            var expectedMd5 = Hex(MD5.HashData(bytes));
            var expectedSha256 = Hex(SHA256.HashData(bytes));
            var legacyHashes = ReadLegacyHashes(legacy, bytes);
            if (legacyHashes.Length == 0)
            {
                Interlocked.Increment(ref legacyFailures);
            }
            foreach (var legacyHash in legacyHashes)
            {
                Interlocked.Increment(ref legacyComparisons);
                if (legacyHash != Convert.ToBase64String(MD5.HashData(bytes)))
                {
                    differences.Add($"{path}: 2.2.3 Hash {legacyHash} is not the Base64 MD5 of the bytes");
                }
            }
            (string Name, string Hash, string Md5, string Sha256)[] results;
            SimaiFile file;
            try
            {
                file = SimaiParser.Parse(new MemoryStream(bytes, false));
                var asyncFile = await SimaiParser.ParseAsync(new MemoryStream(bytes, false));
                var metadata = SimaiParser.ParseMetadata(new MemoryStream(bytes, false));
                var asyncMetadata = await SimaiParser.ParseMetadataAsync(new MemoryStream(bytes, false));
                results = new[]
                {
                    ("Parse", file.Hash, file.Md5, file.Sha256),
                    ("ParseAsync", asyncFile.Hash, asyncFile.Md5, asyncFile.Sha256),
                    ("ParseMetadata", metadata.Hash, metadata.Md5, metadata.Sha256),
                    ("ParseMetadataAsync", asyncMetadata.Hash, asyncMetadata.Md5, asyncMetadata.Sha256),
                };
            }
            catch (InvalidSimaiMarkupException)
            {
                Interlocked.Increment(ref currentFailures);
                return;
            }
            foreach (var result in results)
            {
                foreach (var legacyHash in legacyHashes)
                {
                    if (result.Hash != legacyHash)
                    {
                        differences.Add($"{path}: {result.Name} Hash {result.Hash}, 2.2.3 Hash {legacyHash}");
                    }
                }
                if (result.Md5 != Hex(Convert.FromBase64String(result.Hash)))
                {
                    differences.Add($"{path}: {result.Name} Md5 {result.Md5} is not Hash {result.Hash} in hex");
                }
                if (result.Md5 != expectedMd5)
                {
                    differences.Add($"{path}: {result.Name} Md5 {result.Md5}, expected {expectedMd5}");
                }
                if (result.Sha256 != expectedSha256)
                {
                    differences.Add($"{path}: {result.Name} Sha256 {result.Sha256}, expected {expectedSha256}");
                }
            }
            for (var i = 0; i < file.Charts.Length; i++)
            {
                var chart = file.Charts[i];
                var text = Encoding.UTF8.GetBytes(new string(chart.Fumen.Where(c => !char.IsWhiteSpace(c)).ToArray()));
                if (chart.Md5 != Hex(MD5.HashData(text)) || chart.Sha256 != Hex(SHA256.HashData(text)))
                {
                    differences.Add($"{path}: chart {i + 1} hashes {chart.Md5} / {chart.Sha256} do not match its text");
                }
                Interlocked.Increment(ref charts);
            }
        });
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Files: {files.Length}");
        output?.WriteLine($"2.2.3 hashes compared: {legacyComparisons}");
        output?.WriteLine($"Files 2.2.3 could not parse (checked against MD5 only): {legacyFailures}");
        output?.WriteLine($"Files the current parser rejects: {currentFailures}");
        output?.WriteLine($"Charts checked: {charts}");
        output?.WriteLine($"Differences: {differences.Count}");
        foreach (var difference in differences.OrderBy(d => d, StringComparer.Ordinal).Take(50))
        {
            output?.WriteLine(difference);
        }
        Assert.Empty(differences);
        Assert.True(legacyComparisons > 0, "No file could be compared with 2.2.3.");
    }

    /// <summary>
    /// Reads the hash 2.2.3 gives a file, from both <c>Parse</c> and <c>ParseMetadata</c>.
    /// </summary>
    /// <param name="legacy">The legacy parser.</param>
    /// <param name="bytes">The raw bytes of the file.</param>
    /// <returns>The hashes of the calls that succeeded; empty when 2.2.3 rejects the file.</returns>
    private static string[] ReadLegacyHashes(LegacyParser legacy, byte[] bytes)
    {
        var hashes = new List<string>(2);
        try
        {
            hashes.Add(legacy.ParseFile(bytes, out _).Hash);
        }
        catch (Exception)
        {
            // 2.2.3 rejects some files the current parser accepts; the other call may still succeed.
        }
        try
        {
            hashes.Add(legacy.ParseMetadata(bytes).Hash);
        }
        catch (Exception)
        {
            // Same as above.
        }

        return hashes.ToArray();
    }

    /// <summary>
    /// Writes bytes as a lowercase hex string with the framework API.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The lowercase hex string.</returns>
    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
