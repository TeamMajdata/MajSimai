using System;
using System.Collections.Generic;
using MajSimai.Utils;

namespace MajSimai;

/// <summary>
/// The fields of a simai file (<c>&amp;key=value</c>) before the charts are parsed.
/// </summary>
public readonly struct SimaiMetadata
{
    /// <summary>
    /// The number of difficulties a simai file has (<c>&amp;inote_1</c> to <c>&amp;inote_7</c>).
    /// </summary>
    internal const int ChartCount = 7;

    /// <summary>
    /// The designer of each chart.
    /// </summary>
    private readonly string[] _designers;

    /// <summary>
    /// The level of each chart.
    /// </summary>
    private readonly string[] _levels;

    /// <summary>
    /// The text of each chart.
    /// </summary>
    private readonly string[] _fumens;

    /// <summary>
    /// The other fields of the file.
    /// </summary>
    private readonly SimaiCommand[] _commands;

    /// <summary>
    /// The MD5 and SHA-256 hashes of the file, or <see langword="null"/> for a default value.
    /// </summary>
    private readonly ContentHashes? _hashes;

    /// <summary>
    /// Creates metadata from sequences. Only the first seven designers, levels and charts are kept. There are no file
    /// bytes to hash, so <see cref="Md5"/> and <see cref="Sha256"/> are empty.
    /// </summary>
    /// <param name="title">The song title.</param>
    /// <param name="artist">The song artist.</param>
    /// <param name="offset">The <c>&amp;first</c> offset in seconds.</param>
    /// <param name="designers">The designer of each chart.</param>
    /// <param name="levels">The level of each chart.</param>
    /// <param name="fumens">The text of each chart.</param>
    /// <param name="commands">The other fields of the file.</param>
    /// <param name="hash">The hash of the file content.</param>
    /// <param name="finalDesigner">The designer shown for the whole file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hash"/> is empty.</exception>
    public SimaiMetadata(string title,
                         string artist,
                         float offset,
                         IEnumerable<string>? designers,
                         IEnumerable<string>? levels,
                         IEnumerable<string>? fumens,
                         IEnumerable<SimaiCommand>? commands,
                         string hash,
                         string finalDesigner = "")
    {
        if (hash is null)
        {
            throw new ArgumentNullException(nameof(hash));
        }
        if (hash.Length == 0)
        {
            throw new ArgumentException("The hash cannot be empty.", nameof(hash));
        }
        Title = title;
        Artist = artist;
        Offset = offset;
        Hash = hash;
        FinalDesigner = finalDesigner;
        _designers = TakeSeven(designers);
        _levels = TakeSeven(levels);
        _fumens = TakeSeven(fumens);
        _commands = commands is null ? Array.Empty<SimaiCommand>() : new List<SimaiCommand>(commands).ToArray();
        _hashes = ContentHashes.None;
    }

    /// <summary>
    /// Creates metadata from spans. Only the first seven designers, levels and charts are kept. There are no file bytes
    /// to hash, so <see cref="Md5"/> and <see cref="Sha256"/> are empty.
    /// </summary>
    /// <param name="title">The song title.</param>
    /// <param name="artist">The song artist.</param>
    /// <param name="offset">The <c>&amp;first</c> offset in seconds.</param>
    /// <param name="designers">The designer of each chart.</param>
    /// <param name="levels">The level of each chart.</param>
    /// <param name="fumens">The text of each chart.</param>
    /// <param name="commands">The other fields of the file.</param>
    /// <param name="hash">The hash of the file content.</param>
    /// <param name="finalDesigner">The designer shown for the whole file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    public SimaiMetadata(string title,
                         string artist,
                         float offset,
                         ReadOnlySpan<string> designers,
                         ReadOnlySpan<string> levels,
                         ReadOnlySpan<string> fumens,
                         ReadOnlySpan<SimaiCommand> commands,
                         string hash,
                         string finalDesigner = "")
        : this(title,
               artist,
               offset,
               TakeSeven(designers),
               TakeSeven(levels),
               TakeSeven(fumens),
               commands.IsEmpty ? Array.Empty<SimaiCommand>() : commands.ToArray(),
               hash ?? throw new ArgumentNullException(nameof(hash)),
               ContentHashes.None,
               finalDesigner)
    {
    }

    /// <summary>
    /// Creates metadata that keeps the given arrays.
    /// </summary>
    private SimaiMetadata(string title,
                          string artist,
                          float offset,
                          string[] designers,
                          string[] levels,
                          string[] fumens,
                          SimaiCommand[] commands,
                          string hash,
                          ContentHashes hashes,
                          string finalDesigner)
    {
        Title = title;
        Artist = artist;
        Offset = offset;
        _designers = designers;
        _levels = levels;
        _fumens = fumens;
        _commands = commands;
        Hash = hash;
        _hashes = hashes;
        FinalDesigner = finalDesigner;
    }

    /// <summary>
    /// Gets the song title (<c>&amp;title</c>).
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the song artist (<c>&amp;artist</c>).
    /// </summary>
    public string Artist { get; }

    /// <summary>
    /// Gets the designer shown for the whole file: <c>&amp;des</c>, or else the last chart designer that is set.
    /// </summary>
    public string FinalDesigner { get; }

    /// <summary>
    /// Gets the <c>&amp;first</c> offset in seconds.
    /// </summary>
    public float Offset { get; }

    /// <summary>
    /// Gets the designer of each of the seven charts (<c>&amp;des_1</c> to <c>&amp;des_7</c>).
    /// </summary>
    public ReadOnlySpan<string> Designers => _designers;

    /// <summary>
    /// Gets the level of each of the seven charts (<c>&amp;lv_1</c> to <c>&amp;lv_7</c>).
    /// </summary>
    public ReadOnlySpan<string> Levels => _levels;

    /// <summary>
    /// Gets the text of each of the seven charts (<c>&amp;inote_1</c> to <c>&amp;inote_7</c>).
    /// </summary>
    public ReadOnlySpan<string> Fumens => _fumens;

    /// <summary>
    /// Gets the other fields of the file, in text order.
    /// </summary>
    public ReadOnlySpan<SimaiCommand> Commands => _commands;

    /// <summary>
    /// Gets the hash of the file content.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Gets the MD5 hash of the file as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// Read from a stream, it is the hash of the original bytes and matches <see cref="Hash"/>, which is the same MD5 in
    /// Base64. Read from text, it is the hash of the UTF-8 bytes of the text. Metadata created with a constructor has an
    /// empty string.
    /// </remarks>
    public string Md5 => (_hashes ?? ContentHashes.None).Md5;

    /// <summary>
    /// Gets the SHA-256 hash of the file as a lowercase hex string, from the same bytes as <see cref="Md5"/>.
    /// </summary>
    /// <remarks>
    /// Read from a stream, it is the hash of the original bytes. Read from text, it is the hash of the UTF-8 bytes of the
    /// text. Metadata created with a constructor has an empty string.
    /// </remarks>
    public string Sha256 => (_hashes ?? ContentHashes.None).Sha256;

    /// <summary>
    /// Creates metadata that keeps the given arrays instead of copying them.
    /// </summary>
    /// <param name="title">The song title.</param>
    /// <param name="artist">The song artist.</param>
    /// <param name="offset">The <c>&amp;first</c> offset in seconds.</param>
    /// <param name="designers">Seven designers; the array must not be changed afterwards.</param>
    /// <param name="levels">Seven levels; the array must not be changed afterwards.</param>
    /// <param name="fumens">Seven chart texts; the array must not be changed afterwards.</param>
    /// <param name="commands">The other fields; the array must not be changed afterwards.</param>
    /// <param name="hash">The hash of the file content.</param>
    /// <param name="hashes">The MD5 and SHA-256 hashes of the file.</param>
    /// <param name="finalDesigner">The designer shown for the whole file.</param>
    /// <returns>The metadata.</returns>
    internal static SimaiMetadata Create(string title,
                                         string artist,
                                         float offset,
                                         string[] designers,
                                         string[] levels,
                                         string[] fumens,
                                         SimaiCommand[] commands,
                                         string hash,
                                         ContentHashes hashes,
                                         string finalDesigner)
    {
        return new SimaiMetadata(title, artist, offset, designers, levels, fumens, commands, hash, hashes, finalDesigner);
    }

    /// <summary>
    /// Copies at most seven values into a new array of seven.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The array; missing values are null.</returns>
    private static string[] TakeSeven(IEnumerable<string>? values)
    {
        var result = new string[ChartCount];
        if (values is null)
        {
            return result;
        }
        var i = 0;
        foreach (var value in values)
        {
            result[i++] = value;
            if (i == ChartCount)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Copies at most seven values into a new array of seven.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The array; missing values are null.</returns>
    private static string[] TakeSeven(ReadOnlySpan<string> values)
    {
        var result = new string[ChartCount];
        if (values.Length > ChartCount)
        {
            values = values.Slice(0, ChartCount);
        }
        values.CopyTo(result);

        return result;
    }
}
