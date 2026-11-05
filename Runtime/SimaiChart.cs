using System;
using System.Threading;
using MajSimai.Parsing;
using MajSimai.Utils;

namespace MajSimai;

/// <summary>
/// One difficulty of a simai file: the chart text and the timing points read from it.
/// </summary>
public sealed class SimaiChart
{
    /// <summary>
    /// A chart without a level, designer, text or timing points.
    /// </summary>
    public static readonly SimaiChart Empty = new(string.Empty,
                                                  string.Empty,
                                                  string.Empty,
                                                  Array.Empty<SimaiTimingPoint>(),
                                                  Array.Empty<SimaiTimingPoint>());

    /// <summary>
    /// The timing points that have notes.
    /// </summary>
    private readonly SimaiTimingPoint[] _noteTimings;

    /// <summary>
    /// The values of the comma timing points that are not created yet.
    /// </summary>
    private readonly CommaTiming[] _commaData;

    /// <summary>
    /// The comma timing points, or null until they are first read.
    /// </summary>
    private SimaiTimingPoint[]? _commaTimings;

    /// <summary>
    /// The MD5 and SHA-256 hashes of the chart text, or null until they are first read.
    /// </summary>
    private ContentHashes? _hashes;

    /// <summary>
    /// Creates a chart without comma timing points.
    /// </summary>
    /// <param name="level">The level text, such as <c>13+</c>.</param>
    /// <param name="designer">The chart designer.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="noteTimings">The timing points that have notes; they are copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a timing point is <see langword="null"/>, or when there are timing points but <paramref name="fumen"/> is empty.</exception>
    public SimaiChart(string level, string designer, string fumen, ReadOnlySpan<SimaiTimingPoint> noteTimings)
        : this(level, designer, fumen, noteTimings, ReadOnlySpan<SimaiTimingPoint>.Empty)
    {
    }

    /// <summary>
    /// Creates a chart.
    /// </summary>
    /// <param name="level">The level text, such as <c>13+</c>.</param>
    /// <param name="designer">The chart designer.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="noteTimings">The timing points that have notes; they are copied.</param>
    /// <param name="commaTimings">The timing points of every comma; they are copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a timing point is <see langword="null"/>, or when there are note timing points but <paramref name="fumen"/> is empty.</exception>
    public SimaiChart(string level,
                      string designer,
                      string fumen,
                      ReadOnlySpan<SimaiTimingPoint> noteTimings,
                      ReadOnlySpan<SimaiTimingPoint> commaTimings)
        : this(level, designer, fumen, CopyTimings(noteTimings, nameof(noteTimings)), CopyTimings(commaTimings, nameof(commaTimings)))
    {
        if (_noteTimings.Length != 0 && string.IsNullOrEmpty(fumen))
        {
            throw new ArgumentException("A chart with timing points must have a chart text.", nameof(fumen));
        }
    }

    /// <summary>
    /// Creates a chart that keeps the given arrays.
    /// </summary>
    private SimaiChart(string level, string designer, string? fumen, SimaiTimingPoint[] noteTimings, SimaiTimingPoint[] commaTimings)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Designer = designer ?? throw new ArgumentNullException(nameof(designer));
        Fumen = fumen ?? string.Empty;
        _noteTimings = noteTimings;
        _commaTimings = commaTimings;
        _commaData = Array.Empty<CommaTiming>();
    }

    /// <summary>
    /// Creates a chart whose comma timing points are created when <see cref="CommaTimings"/> is first read.
    /// </summary>
    private SimaiChart(string level, string designer, string fumen, SimaiTimingPoint[] noteTimings, CommaTiming[] commaData)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Designer = designer ?? throw new ArgumentNullException(nameof(designer));
        Fumen = fumen ?? string.Empty;
        _noteTimings = noteTimings;
        _commaData = commaData;
        _commaTimings = commaData.Length == 0 ? Array.Empty<SimaiTimingPoint>() : null;
    }

    /// <summary>
    /// Gets the level text, such as <c>13+</c>.
    /// </summary>
    public string Level { get; }

    /// <summary>
    /// Gets the chart designer.
    /// </summary>
    public string Designer { get; }

    /// <summary>
    /// Gets the chart text.
    /// </summary>
    public string Fumen { get; }

    /// <summary>
    /// Gets whether the chart has no note timing points.
    /// </summary>
    public bool IsEmpty => _noteTimings.Length == 0;

    /// <summary>
    /// Gets the timing points that have notes, in text order.
    /// </summary>
    public ReadOnlySpan<SimaiTimingPoint> NoteTimings => _noteTimings;

    /// <summary>
    /// Gets the timing points of every comma, plus one for the end of the text.
    /// </summary>
    /// <remarks>
    /// For a parsed chart the timing point objects are created on first access, which is thread safe.
    /// </remarks>
    public ReadOnlySpan<SimaiTimingPoint> CommaTimings => _commaTimings ?? CreateCommaTimings();

    /// <summary>
    /// Gets the MD5 hash of the chart text as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// The text is hashed as one line: every white-space character (<see cref="char.IsWhiteSpace(char)"/>), line breaks
    /// included, is removed, and the UTF-8 bytes of what is left are hashed. Charts that differ only in white space have
    /// the same hash. The hash is computed on first access, which is thread safe.
    /// </remarks>
    public string Md5 => GetHashes().Md5;

    /// <summary>
    /// Gets the SHA-256 hash of the chart text as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// The text is hashed the same way as for <see cref="Md5"/>: as one line without white space, in UTF-8. The hash is
    /// computed on first access, which is thread safe.
    /// </remarks>
    public string Sha256 => GetHashes().Sha256;

    /// <summary>
    /// Creates a parsed chart that keeps the given arrays instead of copying them.
    /// </summary>
    /// <param name="level">The level text.</param>
    /// <param name="designer">The chart designer.</param>
    /// <param name="fumen">The chart text.</param>
    /// <param name="noteTimings">The note timing points; the array must not be changed afterwards.</param>
    /// <param name="commaTimings">The values of the comma timing points; the array must not be changed afterwards.</param>
    /// <returns>The chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="designer"/> is <see langword="null"/>.</exception>
    internal static SimaiChart Create(string level,
                                      string designer,
                                      string fumen,
                                      SimaiTimingPoint[] noteTimings,
                                      CommaTiming[] commaTimings)
    {
        return new SimaiChart(level, designer, fumen, noteTimings, commaTimings);
    }

    /// <summary>
    /// Gets the values of the comma timing points while their objects are not created yet, so a caller that only reads
    /// the values, such as the native converter, does not create one object per comma. Each value matches the object
    /// <see cref="CommaTimings"/> would create: no notes and an empty raw content.
    /// </summary>
    /// <param name="commaData">Receives the values, or an empty span when the method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the objects are not created yet; <see langword="false"/> when they are, or when the
    /// chart was created with timing point objects, and <see cref="CommaTimings"/> must be read instead.
    /// </returns>
    internal bool TryGetCommaData(out ReadOnlySpan<CommaTiming> commaData)
    {
        if (Volatile.Read(ref _commaTimings) is null)
        {
            commaData = _commaData;
            return true;
        }
        commaData = ReadOnlySpan<CommaTiming>.Empty;
        return false;
    }

    /// <summary>
    /// Creates the comma timing point objects from their stored values. When several threads race, all of them return
    /// the array that was stored first.
    /// </summary>
    private SimaiTimingPoint[] CreateCommaTimings()
    {
        var timings = new SimaiTimingPoint[_commaData.Length];
        for (var i = 0; i < timings.Length; i++)
        {
            timings[i] = _commaData[i].ToTimingPoint();
        }

        return Interlocked.CompareExchange(ref _commaTimings, timings, null) ?? timings;
    }

    /// <summary>
    /// Gets the hashes of the chart text, computing them on first use. When several threads race, all of them return
    /// the value that was stored first.
    /// </summary>
    /// <returns>The hashes of <see cref="Fumen"/> without white space.</returns>
    private ContentHashes GetHashes()
    {
        var hashes = Volatile.Read(ref _hashes);
        if (hashes is not null)
        {
            return hashes;
        }
        hashes = ContentHashes.FromFumen(Fumen.AsSpan());

        return Interlocked.CompareExchange(ref _hashes, hashes, null) ?? hashes;
    }

    /// <summary>
    /// Copies timing points and checks that none is null.
    /// </summary>
    /// <param name="timings">The timing points.</param>
    /// <param name="parameterName">The parameter name used in the exception.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">Thrown when a timing point is <see langword="null"/>.</exception>
    private static SimaiTimingPoint[] CopyTimings(ReadOnlySpan<SimaiTimingPoint> timings, string parameterName)
    {
        if (timings.IsEmpty)
        {
            return Array.Empty<SimaiTimingPoint>();
        }
        foreach (var timing in timings)
        {
            if (timing is null)
            {
                throw new ArgumentException("Timing points cannot be null.", parameterName);
            }
        }

        return timings.ToArray();
    }
}
