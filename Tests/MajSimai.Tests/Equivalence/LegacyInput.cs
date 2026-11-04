using System;
using System.Text;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Rewrites input for MajSimai 2.2.3 so that, where 3.x fixed a 2.x bug on purpose, 2.x reads it with the 3.x meaning.
/// The rewritten input is only given to the legacy parser.
/// </summary>
internal static class LegacyInput
{
    /// <summary>
    /// Rewrites a chart text.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="paddedComment">Set when a space was added after a final <c>||</c>, which moves the last comma timing
    /// point of the 2.x result one character to the right.</param>
    /// <returns>The chart text for the legacy parser.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The text is cut at the end marker <c>E</c>, which 3.x honors and 2.x read as note text.</item>
    /// <item>A <c>||</c> at the very end gets a space after it, because 2.x read past the end of the text there.</item>
    /// </list>
    /// </remarks>
    public static string CanonicalizeChart(string fumen, out bool paddedComment)
    {
        var end = FindEndMarker(fumen);
        var text = end == -1 ? fumen : fumen.Substring(0, end);
        paddedComment = text.EndsWith("||", StringComparison.Ordinal) && IsCommentStart(text, text.Length - 2);

        return paddedComment ? text + " " : text;
    }

    /// <summary>
    /// Rewrites a simai file: field names and values are trimmed, lines starting with <c>&amp;</c> without <c>=</c> are
    /// replaced by <c>&amp;=</c>, and a <c>&amp;first</c> value with a <c>,</c> is made unreadable (2.x read <c>0,071</c> as 71), as 3.x
    /// reads them in lenient mode.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <returns>The simai text for the legacy parser.</returns>
    public static string CanonicalizeFile(string content)
    {
        var lines = content.Split('\n');
        var builder = new StringBuilder(content.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] != '&')
            {
                builder.Append(line);
                continue;
            }
            var separator = trimmed.IndexOf('=');
            if (separator == -1)
            {
                // Still a field line, so it still ends a chart text, but one both parsers ignore.
                builder.Append("&=");
                continue;
            }
            var name = trimmed.Substring(1, separator - 1).Trim();
            var value = trimmed.Substring(separator + 1).Trim();
            if (name == "first" && value.Contains(','))
            {
                value = "not a number";
            }
            builder.Append('&').Append(name).Append('=').Append(value);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Finds the end marker <c>E</c> the way the 3.x scanner does: an <c>E</c> where note text starts, not followed by a
    /// digit, outside comments and declarations.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The index of the end marker, or -1.</returns>
    private static int FindEndMarker(string fumen)
    {
        var hasNote = false;
        for (var i = 0; i < fumen.Length; i++)
        {
            var c = fumen[i];
            switch (c)
            {
                case '|' when IsCommentStart(fumen, i):
                    var lineEnd = fumen.IndexOf('\n', i);
                    if (lineEnd == -1)
                    {
                        return -1;
                    }
                    i = lineEnd;
                    continue;
                case '(':
                case '{':
                    hasNote = false;
                    var close = fumen.IndexOf(c == '(' ? ')' : '}', i + 1);
                    if (close == -1)
                    {
                        return -1;
                    }
                    i = close;
                    continue;
                case '<' when !hasNote:
                    var speedClose = fumen.IndexOf('>', i + 1);
                    if (speedClose == -1)
                    {
                        return -1;
                    }
                    i = speedClose;
                    continue;
                case ',':
                    hasNote = false;
                    continue;
            }
            if (hasNote || !((c >= '0' && c <= '9') || (c >= 'A' && c <= 'E')))
            {
                continue;
            }
            if (c == 'E' && !IsFollowedByDigit(fumen, i))
            {
                return i;
            }
            hasNote = true;
        }

        return -1;
    }

    /// <summary>
    /// Gets whether a <c>|</c> starts a <c>||</c> comment.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index of the <c>|</c>.</param>
    /// <returns><see langword="true"/> when the next character is also <c>|</c>.</returns>
    private static bool IsCommentStart(string text, int index)
    {
        return index + 1 < text.Length && text[index] == '|' && text[index + 1] == '|';
    }

    /// <summary>
    /// Gets whether the next character that is not whitespace is a digit.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index before the characters to look at.</param>
    /// <returns><see langword="true"/> when a digit follows.</returns>
    private static bool IsFollowedByDigit(string text, int index)
    {
        for (var i = index + 1; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i] >= '0' && text[i] <= '9';
            }
        }

        return false;
    }
}
