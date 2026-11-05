using System;
using System.Collections.Generic;

namespace MajSimai;

/// <summary>
/// A parsed simai file (<c>maidata.txt</c>): the song fields and seven charts.
/// </summary>
public sealed class SimaiFile
{
    /// <summary>
    /// The seven charts.
    /// </summary>
    private readonly SimaiChart[] _charts = new SimaiChart[SimaiMetadata.ChartCount];

    /// <summary>
    /// The other fields of the file.
    /// </summary>
    private readonly List<SimaiCommand> _commands = new();

    /// <summary>
    /// Creates a simai file. Missing charts are filled with <see cref="SimaiChart.Empty"/>, and only the first seven
    /// charts are kept. There are no file bytes to hash, so <see cref="Md5"/> and <see cref="Sha256"/> start empty.
    /// </summary>
    /// <param name="title">The song title.</param>
    /// <param name="artist">The song artist.</param>
    /// <param name="offset">The <c>&amp;first</c> offset in seconds.</param>
    /// <param name="hash">The hash of the file content.</param>
    /// <param name="levels">The charts, from <c>&amp;inote_1</c> to <c>&amp;inote_7</c>.</param>
    /// <param name="commands">The other fields of the file.</param>
    /// <param name="finalDesigner">The designer shown for the whole file.</param>
    public SimaiFile(string title,
                     string artist,
                     float offset,
                     string hash,
                     IEnumerable<SimaiChart>? levels,
                     IEnumerable<SimaiCommand>? commands,
                     string finalDesigner = "")
    {
        Title = title ?? string.Empty;
        Artist = artist ?? string.Empty;
        FinalDesigner = finalDesigner ?? string.Empty;
        Offset = offset;
        Hash = hash ?? string.Empty;
        Md5 = string.Empty;
        Sha256 = string.Empty;
        _charts.AsSpan().Fill(SimaiChart.Empty);
        if (levels is not null)
        {
            var i = 0;
            foreach (var chart in levels)
            {
                _charts[i++] = chart;
                if (i == _charts.Length)
                {
                    break;
                }
            }
        }
        if (commands is not null)
        {
            _commands.AddRange(commands);
        }
    }

    /// <summary>
    /// Creates a simai file from charts and commands the parser already prepared.
    /// </summary>
    /// <param name="metadata">The fields of the file.</param>
    /// <param name="charts">The seven parsed charts.</param>
    internal SimaiFile(in SimaiMetadata metadata, ReadOnlySpan<SimaiChart> charts)
    {
        Title = metadata.Title ?? string.Empty;
        Artist = metadata.Artist ?? string.Empty;
        FinalDesigner = metadata.FinalDesigner ?? string.Empty;
        Offset = metadata.Offset;
        Hash = metadata.Hash ?? string.Empty;
        Md5 = metadata.Md5;
        Sha256 = metadata.Sha256;
        _charts.AsSpan().Fill(SimaiChart.Empty);
        charts.Slice(0, Math.Min(charts.Length, _charts.Length)).CopyTo(_charts);
        var commands = metadata.Commands;
        _commands.Capacity = commands.Length;
        foreach (var command in commands)
        {
            _commands.Add(command);
        }
    }

    /// <summary>
    /// Gets or sets the song title (<c>&amp;title</c>).
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the song artist (<c>&amp;artist</c>).
    /// </summary>
    public string Artist { get; set; }

    /// <summary>
    /// Gets or sets the designer shown for the whole file (<c>&amp;des</c>).
    /// </summary>
    public string FinalDesigner { get; set; }

    /// <summary>
    /// Gets or sets the <c>&amp;first</c> offset in seconds.
    /// </summary>
    public float Offset { get; set; }

    /// <summary>
    /// Gets the seven charts, from <c>&amp;inote_1</c> to <c>&amp;inote_7</c>.
    /// </summary>
    public SimaiChart[] Charts => _charts;

    /// <summary>
    /// Gets or sets the hash of the file content.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets the MD5 hash of the file as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// A file parsed from a stream has the hash of the original bytes, which matches <see cref="Hash"/> (the same MD5 in
    /// Base64). A file parsed from text has the hash of the UTF-8 bytes of the text. A file created with the constructor
    /// starts with an empty string.
    /// </remarks>
    public string Md5 { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the file as a lowercase hex string, from the same bytes as <see cref="Md5"/>.
    /// </summary>
    /// <remarks>
    /// A file parsed from a stream has the hash of the original bytes. A file parsed from text has the hash of the UTF-8
    /// bytes of the text. A file created with the constructor starts with an empty string.
    /// </remarks>
    public string Sha256 { get; set; }

    /// <summary>
    /// Gets the other fields of the file, in text order.
    /// </summary>
    public IList<SimaiCommand> Commands => _commands;

    /// <summary>
    /// Creates a file without charts.
    /// </summary>
    /// <param name="title">The song title.</param>
    /// <param name="artist">The song artist.</param>
    /// <returns>A file whose seven charts are <see cref="SimaiChart.Empty"/>.</returns>
    public static SimaiFile Empty(string title, string artist)
    {
        return new SimaiFile(title, artist, 0, string.Empty, null, null);
    }
}
