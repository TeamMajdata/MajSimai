namespace MajSimai;

/// <summary>
/// How strictly <see cref="SimaiParser"/> follows the simai grammar.
/// </summary>
public enum SimaiSyntaxTolerance
{
    /// <summary>
    /// Reads charts the way MajSimai always has: a note that cannot be read is skipped, and text whose meaning the
    /// grammar does not define (such as note text cut off by <c>(120)</c>, or a last note without a comma) is dropped and
    /// logged. Malformed text that cannot be read in any sensible way still throws.
    /// </summary>
    Lenient,
    /// <summary>
    /// Throws for every note that cannot be read and for every text whose meaning the grammar does not define. A chart
    /// that fails makes the whole file fail.
    /// </summary>
    Strict
}
