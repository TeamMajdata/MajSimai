namespace MajSimai.Parsing;

/// <summary>
/// A timing point that has note text, found by <see cref="ChartScanner"/> before its notes are read.
/// </summary>
internal readonly struct RawNoteTiming
{
    /// <summary>
    /// Creates a raw timing point.
    /// </summary>
    /// <param name="timing">The time in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="hSpeed">The note speed multiplier in effect.</param>
    /// <param name="textPositionX">The column of the comma.</param>
    /// <param name="textPositionY">The line of the comma.</param>
    /// <param name="textPosition">The character offset of the comma.</param>
    /// <param name="contentStart">The start of the note text in the content arena.</param>
    /// <param name="contentLength">The length of the note text in the content arena.</param>
    public RawNoteTiming(double timing,
                         float bpm,
                         float hSpeed,
                         int textPositionX,
                         int textPositionY,
                         int textPosition,
                         int contentStart,
                         int contentLength)
    {
        Timing = timing;
        Bpm = bpm;
        HSpeed = hSpeed;
        TextPositionX = textPositionX;
        TextPositionY = textPositionY;
        TextPosition = textPosition;
        ContentStart = contentStart;
        ContentLength = contentLength;
    }

    /// <summary>Gets the time in seconds.</summary>
    public double Timing { get; }

    /// <summary>Gets the BPM in effect.</summary>
    public float Bpm { get; }

    /// <summary>Gets the note speed multiplier in effect.</summary>
    public float HSpeed { get; }

    /// <summary>Gets the column of the comma.</summary>
    public int TextPositionX { get; }

    /// <summary>Gets the line of the comma.</summary>
    public int TextPositionY { get; }

    /// <summary>Gets the character offset of the comma.</summary>
    public int TextPosition { get; }

    /// <summary>Gets the start of the note text, without whitespace, in the content arena.</summary>
    public int ContentStart { get; }

    /// <summary>Gets the length of the note text in the content arena.</summary>
    public int ContentLength { get; }
}
