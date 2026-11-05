using System;

namespace MajSimai;

/// <summary>
/// A point in time of a chart: either a group of notes that share a time, or the position of a comma.
/// </summary>
public sealed class SimaiTimingPoint
{
    /// <summary>
    /// The notes of the timing point.
    /// </summary>
    private SimaiNote[] _notes;

    /// <summary>
    /// Creates a timing point.
    /// </summary>
    /// <param name="timing">The time in seconds from the start of the chart.</param>
    /// <param name="notes">The notes of the timing point; the array is used as is, not copied.</param>
    /// <param name="rawContent">The note text; whitespace is removed.</param>
    /// <param name="textPosX">The column of the timing point in the chart text.</param>
    /// <param name="textPosY">The line of the timing point in the chart text.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="hspeed">The note speed multiplier in effect.</param>
    /// <param name="sveloc">The slide velocity multiplier in effect.</param>
    /// <param name="rawTextPosition">The character offset of the timing point in the chart text.</param>
    /// <param name="signatureNumerator">The numerator of the time signature in effect.</param>
    /// <param name="signatureDenominator">The denominator of the time signature in effect.</param>
    public SimaiTimingPoint(double timing,
                            SimaiNote[]? notes,
                            ReadOnlySpan<char> rawContent,
                            int textPosX = 0,
                            int textPosY = 0,
                            float bpm = 0f,
                            float hspeed = 1f,
                            float sveloc = 1f,
                            int rawTextPosition = 0,
                            int signatureNumerator = 0,
                            int signatureDenominator = 0)
        : this(timing,
               notes,
               RemoveWhiteSpace(rawContent),
               textPosX,
               textPosY,
               bpm,
               hspeed,
               sveloc,
               rawTextPosition,
               signatureNumerator,
               signatureDenominator,
               true)
    {
    }

    /// <summary>
    /// Creates a timing point from values the parser already prepared, without copying or cleaning them.
    /// </summary>
    private SimaiTimingPoint(double timing,
                             SimaiNote[]? notes,
                             string rawContent,
                             int textPosX,
                             int textPosY,
                             float bpm,
                             float hspeed,
                             float sveloc,
                             int rawTextPosition,
                             int signatureNumerator,
                             int signatureDenominator,
                             bool _)
    {
        Timing = timing;
        _notes = notes ?? Array.Empty<SimaiNote>();
        RawContent = rawContent;
        RawTextPositionX = textPosX;
        RawTextPositionY = textPosY;
        Bpm = bpm;
        HSpeed = hspeed;
        SVeloc = sveloc;
        RawTextPosition = rawTextPosition;
        SignatureNumerator = signatureNumerator;
        SignatureDenominator = signatureDenominator;
    }

    /// <summary>
    /// Gets or sets the time in seconds from the start of the chart.
    /// </summary>
    public double Timing { get; set; }

    /// <summary>
    /// Gets or sets the BPM in effect.
    /// </summary>
    public float Bpm { get; set; }

    /// <summary>
    /// Gets or sets the note speed multiplier (<c>&lt;HS*x&gt;</c>) in effect. Comma timing points always use 1.
    /// </summary>
    public float HSpeed { get; set; }

    /// <summary>
    /// Gets or sets the slide velocity multiplier (<c>&lt;SV*x&gt;</c>) in effect. Note timing points always use 1.
    /// </summary>
    public float SVeloc { get; set; }

    /// <summary>
    /// Gets the note text of the timing point without whitespace, or an empty string for a comma timing point.
    /// </summary>
    public string RawContent { get; }

    /// <summary>
    /// Gets the column of the timing point in the chart text.
    /// </summary>
    public int RawTextPositionX { get; }

    /// <summary>
    /// Gets the line (starting from 1) of the timing point in the chart text.
    /// </summary>
    public int RawTextPositionY { get; }

    /// <summary>
    /// Gets the character offset of the comma that ends the timing point in the chart text.
    /// </summary>
    public int RawTextPosition { get; }

    /// <summary>
    /// Gets the numerator of the time signature (<c>||s3/4</c>) in effect. Note timing points always use 0.
    /// </summary>
    public int SignatureNumerator { get; }

    /// <summary>
    /// Gets the denominator of the time signature (<c>||s3/4</c>) in effect. Note timing points always use 0.
    /// </summary>
    public int SignatureDenominator { get; }

    /// <summary>
    /// Gets or sets the notes of the timing point.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    public SimaiNote[] Notes
    {
        get => _notes;
        set => _notes = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets whether the timing point has no notes.
    /// </summary>
    public bool IsEmpty => _notes.Length == 0;

    /// <summary>
    /// Creates a timing point from values the parser already prepared, without copying the notes or cleaning the text.
    /// </summary>
    /// <param name="timing">The time in seconds from the start of the chart.</param>
    /// <param name="notes">The notes; the array is kept.</param>
    /// <param name="rawContent">The note text, already without whitespace.</param>
    /// <param name="textPosX">The column in the chart text.</param>
    /// <param name="textPosY">The line in the chart text.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="hspeed">The note speed multiplier in effect.</param>
    /// <param name="sveloc">The slide velocity multiplier in effect.</param>
    /// <param name="rawTextPosition">The character offset in the chart text.</param>
    /// <param name="signatureNumerator">The numerator of the time signature.</param>
    /// <param name="signatureDenominator">The denominator of the time signature.</param>
    /// <returns>The timing point.</returns>
    internal static SimaiTimingPoint Create(double timing,
                                            SimaiNote[] notes,
                                            string rawContent,
                                            int textPosX,
                                            int textPosY,
                                            float bpm,
                                            float hspeed,
                                            float sveloc,
                                            int rawTextPosition,
                                            int signatureNumerator,
                                            int signatureDenominator)
    {
        return new SimaiTimingPoint(timing,
                                    notes,
                                    rawContent,
                                    textPosX,
                                    textPosY,
                                    bpm,
                                    hspeed,
                                    sveloc,
                                    rawTextPosition,
                                    signatureNumerator,
                                    signatureDenominator,
                                    true);
    }

    /// <summary>
    /// Removes every whitespace character from a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without whitespace.</returns>
    internal static string RemoveWhiteSpace(ReadOnlySpan<char> text)
    {
        var whiteSpaceIndex = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                whiteSpaceIndex = i;
                break;
            }
        }
        if (whiteSpaceIndex == -1)
        {
            return text.IsEmpty ? string.Empty : new string(text);
        }
        var buffer = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
        var length = 0;
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                buffer[length++] = c;
            }
        }

        return new string(buffer.Slice(0, length));
    }
}
