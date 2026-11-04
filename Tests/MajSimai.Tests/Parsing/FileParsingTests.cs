using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MajSimai.Tests.Parsing;

/// <summary>
/// Tests parsing whole simai files, their metadata, streams and the deparser.
/// </summary>
public sealed class FileParsingTests
{
    /// <summary>
    /// A simai file with several fields and charts, one of which fails.
    /// </summary>
    private const string File =
        "&title=Song\n" +
        "&artist=Artist\n" +
        "&first=1.25\n" +
        "&des_2=Second\n" +
        "&des_4=Fourth\n" +
        "&lv_2=10\n" +
        "&lv_2=11\n" +
        "&wholebpm=150\n" +
        "&inote_2=(120){4}\n" +
        "  1,2,  \n" +
        "\n" +
        "3,\n" +
        "&inote_4=(abc)1,\n" +
        "&inote_5=\n" +
        "&title=Ignored\n";

    /// <summary>
    /// Metadata: reads fields.
    /// </summary>
    [Fact]
    public void Metadata_ReadsFields()
    {
        var metadata = SimaiParser.ParseMetadata(File.AsSpan(), "hash");

        Assert.Equal("Song", metadata.Title);
        Assert.Equal("Artist", metadata.Artist);
        Assert.Equal(1.25f, metadata.Offset);
        Assert.Equal("hash", metadata.Hash);
        Assert.Equal("Fourth", metadata.FinalDesigner);
        Assert.Equal("10", metadata.Levels[1]);
        Assert.Equal("Second", metadata.Designers[1]);
        Assert.Equal("(120){4}\n1,2,\n\n3,", metadata.Fumens[1]);
        Assert.Equal("(abc)1,", metadata.Fumens[3]);
        Assert.Equal(string.Empty, metadata.Fumens[4]);
        Assert.Equal(7, metadata.Fumens.Length);
        Assert.Equal(new SimaiCommand("wholebpm", "150"), Assert.Single(metadata.Commands.ToArray()));
    }

    /// <summary>
    /// Metadata: des wins over chart designers.
    /// </summary>
    [Fact]
    public void Metadata_DesWinsOverChartDesigners()
    {
        var metadata = SimaiParser.ParseMetadata("&des_1=a\n&des=b\n&des=c".AsSpan(), string.Empty);

        Assert.Equal("b", metadata.FinalDesigner);
    }

    /// <summary>
    /// Metadata: last first wins.
    /// </summary>
    [Fact]
    public void Metadata_LastFirstWins()
    {
        Assert.Equal(0f, SimaiParser.ParseMetadata("&first=2\n&first=x".AsSpan(), string.Empty).Offset);
        Assert.Equal(3f, SimaiParser.ParseMetadata("&first=x\n&first=3".AsSpan(), string.Empty).Offset);
    }

    /// <summary>
    /// Metadata: line without equals throws.
    /// </summary>
    [Fact]
    public void Metadata_LineWithoutEquals_Throws()
    {
        var error = Assert.Throws<InvalidSimaiMarkupException>(() => SimaiParser.ParseMetadata("&title=a\n&broken".AsSpan(), string.Empty));

        Assert.Equal(2, error.Line);
        Assert.Equal("&broken", error.Content);
    }

    /// <summary>
    /// Metadata: null hash throws.
    /// </summary>
    [Fact]
    public void Metadata_NullHash_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SimaiParser.ParseMetadata("&title=a".AsSpan(), null!));
    }

    /// <summary>
    /// Parse: keeps failed chart without timings.
    /// </summary>
    [Fact]
    public void Parse_KeepsFailedChartWithoutTimings()
    {
        var file = SimaiParser.Parse(File.AsSpan(), "hash");

        Assert.Equal(7, file.Charts.Length);
        Assert.Equal(3, file.Charts[1].NoteTimings.Length);
        Assert.Equal("10", file.Charts[1].Level);
        Assert.Equal("(abc)1,", file.Charts[3].Fumen);
        Assert.True(file.Charts[3].IsEmpty);
        Assert.True(file.Charts[3].CommaTimings.IsEmpty);
        Assert.Equal("Fourth", file.Charts[3].Designer);
        Assert.True(file.Charts[0].IsEmpty);
        Assert.Equal(new SimaiCommand("wholebpm", "150"), Assert.Single(file.Commands));
        Assert.Equal("hash", file.Hash);
    }

    /// <summary>
    /// Parse stream: hashes bytes and skips byte order mark.
    /// </summary>
    [Fact]
    public void ParseStream_HashesBytesAndSkipsByteOrderMark()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(File)).ToArray();
        using var md5 = MD5.Create();
        var expectedHash = Convert.ToBase64String(md5.ComputeHash(bytes));

        using var stream = new MemoryStream(bytes);
        var file = SimaiParser.Parse(stream);

        Assert.Equal(expectedHash, file.Hash);
        Assert.Equal("Song", file.Title);
    }

    /// <summary>
    /// Parse stream: uses given encoding.
    /// </summary>
    [Fact]
    public void ParseStream_UsesGivenEncoding()
    {
        var encoding = Encoding.Unicode;
        using var stream = new MemoryStream(encoding.GetBytes("&title=曲名\n"));

        Assert.Equal("曲名", SimaiParser.ParseMetadata(stream, encoding).Title);
    }

    /// <summary>
    /// Parse stream: reads streams without length.
    /// </summary>
    [Fact]
    public void ParseStream_ReadsStreamsWithoutLength()
    {
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(File));

        Assert.Equal("Song", SimaiParser.Parse(stream).Title);
    }

    /// <summary>
    /// Async overloads: match sync parse.
    /// </summary>
    [Fact]
    public async Task AsyncOverloads_MatchSyncParse()
    {
        var expected = SimaiParser.Parse(File.AsSpan(), "hash");
        var bytes = Encoding.UTF8.GetBytes(File);
        var files = new[]
        {
            await SimaiParser.ParseAsync(File, "hash"),
            await SimaiParser.ParseAsync(File.AsMemory(), "hash"),
            await SimaiParser.ParseAsync(SimaiParser.ParseMetadata(File.AsSpan(), "hash")),
            await SimaiParser.ParseAsync(new MemoryStream(bytes)),
            await SimaiParser.ParseAsync(new MemoryStream(bytes), Encoding.UTF8),
        };

        foreach (var file in files)
        {
            for (var i = 0; i < 7; i++)
            {
                Assert.Null(ModelComparer.FindDifference(expected.Charts[i], file.Charts[i]));
            }
            Assert.Equal(expected.Title, file.Title);
        }
        Assert.Equal(files[3].Hash, files[4].Hash);
    }

    /// <summary>
    /// Metadata async overloads: match sync parse.
    /// </summary>
    [Fact]
    public async Task MetadataAsyncOverloads_MatchSyncParse()
    {
        var bytes = Encoding.UTF8.GetBytes(File);
        var all = new[]
        {
            await SimaiParser.ParseMetadataAsync(File, "h"),
            await SimaiParser.ParseMetadataAsync(File.AsMemory(), "h"),
            await SimaiParser.ParseMetadataAsync(new MemoryStream(bytes)),
            await SimaiParser.ParseMetadataAsync(new MemoryStream(bytes), Encoding.UTF8),
            SimaiParser.ParseMetadata(new MemoryStream(bytes)),
        };

        Assert.All(all, m => Assert.Equal("(abc)1,", m.Fumens[3]));
    }

    /// <summary>
    /// Deparse: writes fields and charts.
    /// </summary>
    [Fact]
    public void Deparse_WritesFieldsAndCharts()
    {
        var file = SimaiParser.Parse(File.AsSpan(), "hash");
        var text = SimaiParser.Deparse(file);
        var nl = Environment.NewLine;

        Assert.StartsWith($"&title=Song{nl}&artist=Artist{nl}&des=Fourth{nl}&first=1.25{nl}&des_2=Second{nl}&lv_2=10{nl}", text);
        Assert.Contains($"&wholebpm=150{nl}", text);
        Assert.Contains($"&inote_2=(120){{4}}\n1,2,\n\n3,{nl}", text);
        Assert.DoesNotContain("&inote_1", text);
        var reparsed = SimaiParser.Parse(text.AsSpan(), "hash");
        Assert.Equal(file.Charts[1].Fumen, reparsed.Charts[1].Fumen.Replace("\r", string.Empty));
    }

    /// <summary>
    /// Deparse: stream overloads write the same text.
    /// </summary>
    [Fact]
    public async Task Deparse_StreamOverloadsWriteTheSameText()
    {
        var file = SimaiParser.Parse(File.AsSpan(), "hash");
        var expected = SimaiParser.Deparse(file);

        var stream1 = new MemoryStream();
        SimaiParser.Deparse(file, stream1);
        var stream2 = new MemoryStream();
        await SimaiParser.DeparseAsync(file, stream2);
        var stream3 = new MemoryStream();
        await SimaiParser.DeparseAsync(file, stream3, Encoding.Unicode);

        Assert.Equal(expected, await SimaiParser.DeparseAsync(file));
        Assert.Equal(expected, Encoding.UTF8.GetString(stream1.ToArray()).TrimStart('﻿'));
        Assert.Equal(expected, Encoding.UTF8.GetString(stream2.ToArray()).TrimStart('﻿'));
        Assert.Equal(expected, Encoding.Unicode.GetString(stream3.ToArray()).TrimStart('﻿'));
    }

    /// <summary>
    /// Deparse: skips null charts.
    /// </summary>
    [Fact]
    public void Deparse_SkipsNullCharts()
    {
        var file = SimaiFile.Empty("t", "a");
        file.Charts[0] = null!;

        Assert.StartsWith("&title=t", SimaiParser.Deparse(file));
        Assert.Throws<ArgumentNullException>(() => SimaiParser.Deparse(null!));
    }

    /// <summary>
    /// A memory stream that cannot seek and has no length.
    /// </summary>
    private sealed class NonSeekableStream : MemoryStream
    {
        /// <summary>
        /// Creates the stream.
        /// </summary>
        /// <param name="buffer">The content.</param>
        public NonSeekableStream(byte[] buffer)
            : base(buffer)
        {
        }

        /// <summary>
        /// Gets <see langword="false"/>.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Throws, because the stream has no length.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override long Length => throw new NotSupportedException();
    }
}
