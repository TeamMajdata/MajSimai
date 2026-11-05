using System.Collections.Generic;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// The XML documentation of a member, reduced to plain text.
/// </summary>
/// <param name="Summary">The summary, or an empty string.</param>
/// <param name="Parameters">The text of each documented parameter, by parameter name.</param>
/// <param name="Returns">The description of the return value, or an empty string.</param>
internal sealed record DocComment(string Summary, IReadOnlyDictionary<string, string> Parameters, string Returns)
{
    /// <summary>
    /// Gets a comment with no text.
    /// </summary>
    public static DocComment Empty { get; } = new(string.Empty, new Dictionary<string, string>(), string.Empty);

    /// <summary>
    /// Gets the text of a parameter.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The text, or an empty string when the parameter is not documented.</returns>
    public string GetParameter(string name)
    {
        return Parameters.TryGetValue(name, out var text) ? text : string.Empty;
    }
}
