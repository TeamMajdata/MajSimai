namespace MajSimai;

/// <summary>
/// How strictly <see cref="SimaiParser"/> follows the simai grammar.
/// </summary>
public enum SimaiSyntaxTolerance
{
    /// <summary>
    /// Reads charts the way MajSimai always has: a note that cannot be read or is malformed is skipped, and text whose
    /// meaning the grammar does not define (such as note text cut off by <c>(120)</c>, or a last note without a comma) is
    /// dropped; both are logged. Only declarations that are not closed or whose value cannot be read, and a single
    /// <c>|</c>, still throw.
    /// </summary>
    Lenient,
    /// <summary>
    /// Throws for every note that cannot be read and for every text whose meaning the grammar does not define. A chart
    /// that fails makes the whole file fail.
    /// </summary>
    Strict
}
