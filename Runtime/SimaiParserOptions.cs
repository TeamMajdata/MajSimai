using System;

namespace MajSimai;

/// <summary>
/// Options for <see cref="SimaiParser"/>.
/// </summary>
public sealed class SimaiParserOptions
{
    /// <summary>
    /// Creates options.
    /// </summary>
    /// <param name="tolerance">How strictly the simai grammar is followed.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not a defined value.</exception>
    public SimaiParserOptions(SimaiSyntaxTolerance tolerance = SimaiSyntaxTolerance.Lenient)
    {
        if (tolerance != SimaiSyntaxTolerance.Lenient && tolerance != SimaiSyntaxTolerance.Strict)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
        Tolerance = tolerance;
    }

    /// <summary>
    /// Gets the default options: <see cref="SimaiSyntaxTolerance.Lenient"/>.
    /// </summary>
    public static SimaiParserOptions Lenient { get; } = new(SimaiSyntaxTolerance.Lenient);

    /// <summary>
    /// Gets options with <see cref="SimaiSyntaxTolerance.Strict"/>.
    /// </summary>
    public static SimaiParserOptions Strict { get; } = new(SimaiSyntaxTolerance.Strict);

    /// <summary>
    /// Gets how strictly the simai grammar is followed.
    /// </summary>
    public SimaiSyntaxTolerance Tolerance { get; }

    /// <summary>
    /// Gets whether <see cref="Tolerance"/> is <see cref="SimaiSyntaxTolerance.Strict"/>.
    /// </summary>
    public bool IsStrict => Tolerance == SimaiSyntaxTolerance.Strict;
}
