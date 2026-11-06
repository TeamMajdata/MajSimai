using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MajSimai.Utils;
using Xunit;

namespace MajSimai.Tests.Model;

/// <summary>
/// Tests the MD5 and SHA-256 hashes of <see cref="SimaiFile"/>, <see cref="SimaiMetadata"/> and <see cref="SimaiChart"/>.
/// </summary>
public sealed class HashTests
{
    /// <summary>
    /// A simai file with two charts, written with CRLF line breaks and non-ASCII text.
    /// </summary>
    private const string File =
        "&title=曲名\r\n" +
        "&first=0.5\r\n" +
        "&inote_1=(120){4}\r\n" +
        "1, 2,\r\n" +
        "3,\r\n" +
        "&inote_5=(150){8}1-5[8:1],E\r\n";

    /// <summary>
    /// The MD5 hash of an empty input.
    /// </summary>
    private const string EmptyMd5 = "d41d8cd98f00b204e9800998ecf8427e";

    /// <summary>
    /// The SHA-256 hash of an empty input.
    /// </summary>
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>
    /// Content hashes: match the published MD5 and SHA-256 test vectors.
    /// </summary>
    [Fact]
    public void ContentHashes_MatchKnownVectors()
    {
        var abc = ContentHashes.FromText("abc".AsSpan());
        var empty = ContentHashes.FromBytes(ReadOnlySpan<byte>.Empty);

        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", abc.Md5);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", abc.Sha256);
        Assert.Equal("kAFQmDzST7DWlj99KOF/cg==", abc.Md5Base64);
        Assert.Equal(EmptyMd5, empty.Md5);
        Assert.Equal(EmptySha256, empty.Sha256);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (ContentHashes.None.Md5, ContentHashes.None.Md5Base64, ContentHashes.None.Sha256));
    }

    /// <summary>
    /// Content hashes: text is hashed as UTF-8.
    /// </summary>
    [Fact]
    public void ContentHashes_HashTextAsUtf8()
    {
        var hashes = ContentHashes.FromText("曲名".AsSpan());

        Assert.Equal(Md5Hex(Encoding.UTF8.GetBytes("曲名")), hashes.Md5);
        Assert.Equal(Sha256Hex(Encoding.UTF8.GetBytes("曲名")), hashes.Sha256);
    }

    /// <summary>
    /// Content hashes: a chart text loses every kind of white space before it is hashed.
    /// </summary>
    [Fact]
    public void ContentHashes_FromFumenRemovesAllWhiteSpace()
    {
        var hashes = ContentHashes.FromFumen(" (120){4}\r\n1,\t2,\u3000\n\u00A03,\u2028E ".AsSpan());
        var expected = ContentHashes.FromText("(120){4}1,2,3,E".AsSpan());

        Assert.Equal(expected.Md5, hashes.Md5);
        Assert.Equal(expected.Sha256, hashes.Sha256);
        Assert.Equal(EmptyMd5, ContentHashes.FromFumen(" \r\n\t".AsSpan()).Md5);
    }

    /// <summary>
    /// Content hashes: a chart text loses its <c>||</c> comments, each up to the end of its line, before white space is
    /// removed, so the text after a comment's line break is kept.
    /// </summary>
    [Fact]
    public void ContentHashes_FromFumenRemovesCommentsBeforeWhiteSpace()
    {
        var hashes = ContentHashes.FromFumen("(120){4}||intro 1,2,\r\n1,||note\n2, || spaced\r\n||s3/4\n3,E||end".AsSpan());
        var expected = ContentHashes.FromText("(120){4}1,2,3,E".AsSpan());

        Assert.Equal(expected.Md5, hashes.Md5);
        Assert.Equal(expected.Sha256, hashes.Sha256);
        Assert.Equal(EmptyMd5, ContentHashes.FromFumen("||only a comment".AsSpan()).Md5);
        Assert.Equal(EmptyMd5, ContentHashes.FromFumen("||".AsSpan()).Md5);
        Assert.Equal(EmptyMd5, ContentHashes.FromFumen("|| a\n\t|| b\r\n".AsSpan()).Md5);
    }

    /// <summary>
    /// Content hashes: a single <c>|</c> does not start a comment and is hashed, and <c>|||</c> starts a comment.
    /// </summary>
    [Fact]
    public void ContentHashes_FromFumenKeepsSinglePipe()
    {
        Assert.Equal(ContentHashes.FromText("1|2,".AsSpan()).Md5, ContentHashes.FromFumen("1 | 2,".AsSpan()).Md5);
        Assert.Equal(ContentHashes.FromText("1,|".AsSpan()).Md5, ContentHashes.FromFumen("1,|".AsSpan()).Md5);
        Assert.Equal(ContentHashes.FromText("1,2,".AsSpan()).Md5, ContentHashes.FromFumen("1,|||x\n2,".AsSpan()).Md5);
    }

    /// <summary>
    /// Chart: the hashes are lowercase hex of the UTF-8 single-line chart text.
    /// </summary>
    [Fact]
    public void Chart_HashesSingleLineText()
    {
        const string fumen = "(120){4}\r\n1, 2,\n\t3,\u3000E";
        var chart = SimaiParser.ParseChart(fumen.AsSpan());
        var expectedBytes = Encoding.UTF8.GetBytes(new string(fumen.Where(c => !char.IsWhiteSpace(c)).ToArray()));

        Assert.Equal(Md5Hex(expectedBytes), chart.Md5);
        Assert.Equal(Sha256Hex(expectedBytes), chart.Sha256);
        Assert.Equal(32, chart.Md5.Length);
        Assert.Equal(64, chart.Sha256.Length);
        Assert.Equal(chart.Md5.ToLowerInvariant(), chart.Md5);
    }

    /// <summary>
    /// Chart: charts that differ only in white space have the same hashes, other charts do not.
    /// </summary>
    [Fact]
    public void Chart_IgnoresOnlyWhiteSpace()
    {
        var compact = SimaiParser.ParseChart("(120){4}1,2,3,".AsSpan());
        var spaced = SimaiParser.ParseChart("(120) {4}\n1,\r\n2, 3,\n".AsSpan());
        var different = SimaiParser.ParseChart("(120){4}1,2,4,".AsSpan());

        Assert.Equal(compact.Md5, spaced.Md5);
        Assert.Equal(compact.Sha256, spaced.Sha256);
        Assert.NotEqual(compact.Md5, different.Md5);
        Assert.NotEqual(compact.Sha256, different.Sha256);
    }

    /// <summary>
    /// Chart: charts that differ only in comments have the same hashes, and the text is hashed without comments.
    /// </summary>
    [Fact]
    public void Chart_IgnoresComments()
    {
        const string commented = "||intro\n(120){4}1,|| first bar\r\n2,3,\n||outro";
        var compact = SimaiParser.ParseChart("(120){4}1,2,3,".AsSpan());
        var withComments = SimaiParser.ParseChart(commented.AsSpan());
        var expectedBytes = Encoding.UTF8.GetBytes(NormalizeFumen(commented));

        Assert.Equal("(120){4}1,2,3,", NormalizeFumen(commented));
        Assert.Equal(compact.Md5, withComments.Md5);
        Assert.Equal(compact.Sha256, withComments.Sha256);
        Assert.Equal(Md5Hex(expectedBytes), withComments.Md5);
        Assert.Equal(Sha256Hex(expectedBytes), withComments.Sha256);
    }

    /// <summary>
    /// File: the comments in a chart of a parsed file are not part of its hashes.
    /// </summary>
    [Fact]
    public void File_ChartsHashTextWithoutComments()
    {
        const string file = "&title=t\r\n&inote_1=(120){4}|| comment\r\n1,2,\r\n||s3/4\r\n3,\r\n";
        var parsed = SimaiParser.Parse(file.AsSpan(), "hash");
        var expected = ContentHashes.FromText("(120){4}1,2,3,".AsSpan());

        Assert.Equal(expected.Md5, parsed.Charts[0].Md5);
        Assert.Equal(expected.Sha256, parsed.Charts[0].Sha256);
    }

    /// <summary>
    /// Chart: the hashes depend only on the chart text, not on the level, designer or how the chart was created.
    /// </summary>
    [Fact]
    public void Chart_DependsOnlyOnText()
    {
        const string fumen = "(120){4}1,2,";
        var parsed = SimaiParser.ParseChart("13+", "someone", fumen.AsSpan());
        var created = new SimaiChart(string.Empty, string.Empty, fumen, ReadOnlySpan<SimaiTimingPoint>.Empty);

        Assert.Equal(parsed.Md5, created.Md5);
        Assert.Equal(parsed.Sha256, created.Sha256);
    }

    /// <summary>
    /// Chart: an empty chart has the hashes of an empty input.
    /// </summary>
    [Fact]
    public void Chart_EmptyHasEmptyInputHashes()
    {
        Assert.Equal(EmptyMd5, SimaiChart.Empty.Md5);
        Assert.Equal(EmptySha256, SimaiChart.Empty.Sha256);
        Assert.Equal(EmptyMd5, SimaiParser.Parse(File.AsSpan(), "hash").Charts[1].Md5);
    }

    /// <summary>
    /// Chart: the hashes are computed once, and threads that race get the same strings.
    /// </summary>
    [Fact]
    public void Chart_ComputesHashesOnceAcrossThreads()
    {
        var chart = SimaiParser.ParseChart("(120){4}1,2,3,".AsSpan());
        var results = new (string Md5, string Sha256)[64];

        Parallel.For(0, results.Length, i => results[i] = (chart.Md5, chart.Sha256));

        foreach (var result in results)
        {
            Assert.Same(results[0].Md5, result.Md5);
            Assert.Same(results[0].Sha256, result.Sha256);
        }
        Assert.Same(results[0].Md5, chart.Md5);
    }

    /// <summary>
    /// File: the charts of a parsed file are hashed from their own text.
    /// </summary>
    [Fact]
    public void File_ChartsHashTheirOwnText()
    {
        var file = SimaiParser.Parse(File.AsSpan(), "hash");

        Assert.Equal(ContentHashes.FromText("(120){4}1,2,3,".AsSpan()).Md5, file.Charts[0].Md5);
        Assert.Equal(ContentHashes.FromText("(150){8}1-5[8:1],E".AsSpan()).Sha256, file.Charts[4].Sha256);
    }

    /// <summary>
    /// File from a stream: the hashes come from the original bytes, byte order mark included, and the MD5 matches
    /// <see cref="SimaiFile.Hash"/>.
    /// </summary>
    [Fact]
    public async Task FileFromStream_HashesOriginalBytes()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(File)).ToArray();
        var files = new[]
        {
            SimaiParser.Parse(new MemoryStream(bytes)),
            SimaiParser.Parse(new MemoryStream(bytes), Encoding.UTF8),
            await SimaiParser.ParseAsync(new MemoryStream(bytes)),
            await SimaiParser.ParseAsync(new MemoryStream(bytes), Encoding.UTF8),
        };

        foreach (var file in files)
        {
            Assert.Equal(Md5Hex(bytes), file.Md5);
            Assert.Equal(Sha256Hex(bytes), file.Sha256);
            Assert.Equal(Convert.ToBase64String(Convert.FromHexString(file.Md5)), file.Hash);
            Assert.Equal("曲名", file.Title);
        }
    }

    /// <summary>
    /// Metadata from a stream: the hashes come from the original bytes and match <see cref="SimaiMetadata.Hash"/>.
    /// </summary>
    [Fact]
    public async Task MetadataFromStream_HashesOriginalBytes()
    {
        var encoding = Encoding.Unicode;
        var bytes = encoding.GetBytes(File);
        var metadatas = new[]
        {
            SimaiParser.ParseMetadata(new MemoryStream(bytes), encoding),
            await SimaiParser.ParseMetadataAsync(new MemoryStream(bytes), encoding),
        };

        foreach (var metadata in metadatas)
        {
            Assert.Equal(Md5Hex(bytes), metadata.Md5);
            Assert.Equal(Sha256Hex(bytes), metadata.Sha256);
            Assert.Equal(Convert.ToBase64String(Convert.FromHexString(metadata.Md5)), metadata.Hash);
        }
        Assert.Equal(Md5Hex(bytes), (await SimaiParser.ParseAsync(metadatas[1])).Md5);
    }

    /// <summary>
    /// File from text: the hashes come from the UTF-8 bytes of the text, whatever hash the caller gives.
    /// </summary>
    [Fact]
    public async Task FileFromText_HashesUtf8Text()
    {
        var bytes = Encoding.UTF8.GetBytes(File);
        var metadata = SimaiParser.ParseMetadata(File.AsSpan(), "hash");
        var files = new[]
        {
            SimaiParser.Parse(File.AsSpan(), "hash"),
            SimaiParser.Parse(metadata),
            await SimaiParser.ParseAsync(File, "hash"),
            await SimaiParser.ParseAsync(File.AsMemory(), "hash"),
            await SimaiParser.ParseAsync(metadata),
        };

        Assert.Equal(Md5Hex(bytes), metadata.Md5);
        Assert.Equal(Sha256Hex(bytes), metadata.Sha256);
        Assert.Equal(Md5Hex(bytes), (await SimaiParser.ParseMetadataAsync(File, "hash")).Md5);
        Assert.Equal(Sha256Hex(bytes), (await SimaiParser.ParseMetadataAsync(File.AsMemory(), "hash")).Sha256);
        foreach (var file in files)
        {
            Assert.Equal(Md5Hex(bytes), file.Md5);
            Assert.Equal(Sha256Hex(bytes), file.Sha256);
            Assert.Equal("hash", file.Hash);
        }
    }

    /// <summary>
    /// File from text: a null hash is still rejected.
    /// </summary>
    [Fact]
    public void FileFromText_RejectsNullHash()
    {
        Assert.Throws<ArgumentNullException>(() => SimaiParser.ParseMetadata(File.AsSpan(), null!));
    }

    /// <summary>
    /// File created with a constructor: the hashes start empty and can be set.
    /// </summary>
    [Fact]
    public void FileFromConstructor_StartsEmptyAndCanBeSet()
    {
        var file = new SimaiFile("t", "a", 0, "h", null, null);

        Assert.Equal((string.Empty, string.Empty), (file.Md5, file.Sha256));
        Assert.Equal((string.Empty, string.Empty), (SimaiFile.Empty("t", "a").Md5, SimaiFile.Empty("t", "a").Sha256));
        file.Md5 = "m";
        file.Sha256 = "s";
        Assert.Equal(("m", "s"), (file.Md5, file.Sha256));
    }

    /// <summary>
    /// Metadata created with a constructor or as a default value: the hashes are empty.
    /// </summary>
    [Fact]
    public void MetadataFromConstructor_HasEmptyHashes()
    {
        var fromSequences = new SimaiMetadata("t", "a", 0, (string[]?)null, null, null, null, "h");
        var fromSpans = new SimaiMetadata("t", "a", 0, ReadOnlySpan<string>.Empty, ReadOnlySpan<string>.Empty, ReadOnlySpan<string>.Empty, ReadOnlySpan<SimaiCommand>.Empty, "h");
        var defaultValue = default(SimaiMetadata);

        Assert.Equal((string.Empty, string.Empty), (fromSequences.Md5, fromSequences.Sha256));
        Assert.Equal((string.Empty, string.Empty), (fromSpans.Md5, fromSpans.Sha256));
        Assert.Equal((string.Empty, string.Empty), (defaultValue.Md5, defaultValue.Sha256));
        Assert.Equal(string.Empty, SimaiParser.Parse(fromSequences).Md5);
    }

    /// <summary>
    /// Writes a chart text the way <see cref="SimaiChart.Md5"/> and <see cref="SimaiChart.Sha256"/> hash it, with a
    /// separate implementation: <c>||</c> comments up to the end of their line are removed, then all white space.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The chart text without comments and white space.</returns>
    internal static string NormalizeFumen(string fumen)
    {
        var withoutComments = Regex.Replace(fumen, @"\|\|[^\n]*", string.Empty);

        return new string(withoutComments.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    /// <summary>
    /// Computes an MD5 hash as a lowercase hex string with the framework API.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The lowercase hex hash.</returns>
    private static string Md5Hex(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA-256 hash as a lowercase hex string with the framework API.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The lowercase hex hash.</returns>
    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
