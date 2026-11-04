namespace MajSimai.Parsing;

/// <summary>
/// The values of a comma timing point, kept as a compact struct until <see cref="SimaiChart.CommaTimings"/> is read.
/// </summary>
/// <remarks>
/// Charts often have many more commas than notes, so creating a <see cref="SimaiTimingPoint"/> object for every comma
/// only when a caller asks for them saves most of the memory a parse allocates.
/// </remarks>
internal readonly struct CommaTiming
{
    /// <summary>
    /// Creates comma timing values.
    /// </summary>
    /// <param name="timing">The time in seconds.</param>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="sVeloc">The slide velocity multiplier in effect.</param>
    /// <param name="textPositionX">The column of the comma.</param>
    /// <param name="textPositionY">The line of the comma.</param>
    /// <param name="textPosition">The character offset of the comma.</param>
    /// <param name="signatureNumerator">The numerator of the time signature in effect.</param>
    /// <param name="signatureDenominator">The denominator of the time signature in effect.</param>
    public CommaTiming(double timing,
                       float bpm,
                       float sVeloc,
                       int textPositionX,
                       int textPositionY,
                       int textPosition,
                       int signatureNumerator,
                       int signatureDenominator)
    {
        Timing = timing;
        Bpm = bpm;
        SVeloc = sVeloc;
        TextPositionX = textPositionX;
        TextPositionY = textPositionY;
        TextPosition = textPosition;
        SignatureNumerator = signatureNumerator;
        SignatureDenominator = signatureDenominator;
    }

    /// <summary>Gets the time in seconds.</summary>
    public double Timing { get; }

    /// <summary>Gets the BPM in effect.</summary>
    public float Bpm { get; }

    /// <summary>Gets the slide velocity multiplier in effect.</summary>
    public float SVeloc { get; }

    /// <summary>Gets the column of the comma.</summary>
    public int TextPositionX { get; }

    /// <summary>Gets the line of the comma.</summary>
    public int TextPositionY { get; }

    /// <summary>Gets the character offset of the comma.</summary>
    public int TextPosition { get; }

    /// <summary>Gets the numerator of the time signature in effect.</summary>
    public int SignatureNumerator { get; }

    /// <summary>Gets the denominator of the time signature in effect.</summary>
    public int SignatureDenominator { get; }

    /// <summary>
    /// Creates the timing point object. Comma timing points have no notes and an <see cref="SimaiTimingPoint.HSpeed"/>
    /// of 1.
    /// </summary>
    /// <returns>The timing point.</returns>
    public SimaiTimingPoint ToTimingPoint()
    {
        return SimaiTimingPoint.Create(Timing,
                                       System.Array.Empty<SimaiNote>(),
                                       string.Empty,
                                       TextPositionX,
                                       TextPositionY,
                                       Bpm,
                                       1f,
                                       SVeloc,
                                       TextPosition,
                                       SignatureNumerator,
                                       SignatureDenominator);
    }
}
