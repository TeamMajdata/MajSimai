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
    /// <param name="sVeloc">The slide velocity multiplier in effect.</param>
    /// <param name="signatureNumerator">The numerator of the time signature in effect.</param>
    /// <param name="signatureDenominator">The denominator of the time signature in effect.</param>
    /// <param name="textPositionX">The column of the comma.</param>
    /// <param name="textPositionY">The line of the comma.</param>
    /// <param name="textPosition">The character offset of the comma.</param>
    /// <param name="contentStart">The start of the note text in the content arena.</param>
    /// <param name="contentLength">The length of the note text in the content arena.</param>
    /// <param name="noteLine">The line where the note text starts.</param>
    /// <param name="noteColumn">The column where the note text starts.</param>
    public RawNoteTiming(double timing,
                         float bpm,
                         float hSpeed,
                         float sVeloc,
                         int signatureNumerator,
                         int signatureDenominator,
                         int textPositionX,
                         int textPositionY,
                         int textPosition,
                         int contentStart,
                         int contentLength,
                         int noteLine,
                         int noteColumn)
    {
        Timing = timing;
        Bpm = bpm;
        HSpeed = hSpeed;
        SVeloc = sVeloc;
        SignatureNumerator = signatureNumerator;
        SignatureDenominator = signatureDenominator;
        TextPositionX = textPositionX;
        TextPositionY = textPositionY;
        TextPosition = textPosition;
        ContentStart = contentStart;
        ContentLength = contentLength;
        NoteLine = noteLine;
        NoteColumn = noteColumn;
    }

    /// <summary>Gets the time in seconds.</summary>
    public double Timing { get; }

    /// <summary>Gets the BPM in effect.</summary>
    public float Bpm { get; }

    /// <summary>Gets the note speed multiplier in effect.</summary>
    public float HSpeed { get; }

    /// <summary>Gets the slide velocity multiplier in effect.</summary>
    public float SVeloc { get; }

    /// <summary>Gets the numerator of the time signature in effect.</summary>
    public int SignatureNumerator { get; }

    /// <summary>Gets the denominator of the time signature in effect.</summary>
    public int SignatureDenominator { get; }

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

    /// <summary>Gets the line where the note text starts.</summary>
    public int NoteLine { get; }

    /// <summary>Gets the column where the note text starts.</summary>
    public int NoteColumn { get; }
}
