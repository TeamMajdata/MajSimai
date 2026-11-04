namespace MajSimai.Diagnostics;

/// <summary>
/// How serious a problem found by <see cref="SimaiSyntaxChecker"/> is.
/// </summary>
public enum SimaiDiagnosticSeverity
{
    /// <summary>
    /// The text follows the grammar, but is questionable: hard to read in play, set twice, or only drawn by Majdata.
    /// </summary>
    Warning,
    /// <summary>
    /// The text breaks the grammar. <see cref="SimaiParser"/> rejects it in <see cref="SimaiSyntaxTolerance.Strict"/>
    /// mode; in lenient mode it either fails the chart or is read the MajSimai way (for example a note is skipped).
    /// </summary>
    Error
}
