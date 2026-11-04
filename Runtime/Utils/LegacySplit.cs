using System;

namespace MajSimai.Utils;

/// <summary>
/// The span split used by MajSimai 2.x, kept because its edge cases decide how simai text is read.
/// </summary>
/// <remarks>
/// Unlike <c>MemoryExtensions.Split</c>, this split stops as soon as <c>destination</c> is full. The last range then
/// ends at the separator that filled it, and the rest of the text is ignored. For example, splitting <c>8:1:2</c> by
/// <c>:</c> into two ranges gives <c>8</c> and <c>1</c>.
/// </remarks>
internal static class LegacySplit
{
    /// <summary>
    /// Splits <paramref name="source"/> by <paramref name="separator"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The text to split.</param>
    /// <param name="destination">Receives the ranges of the parts; ranges after the returned count are not touched.</param>
    /// <param name="separator">The separator character.</param>
    /// <param name="removeEmptyEntries">Whether empty parts are skipped.</param>
    /// <returns>The number of ranges written to <paramref name="destination"/>.</returns>
    public static int Split(ReadOnlySpan<char> source, Span<Range> destination, char separator, bool removeEmptyEntries)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }
        if (source.IsEmpty)
        {
            if (removeEmptyEntries)
            {
                return 0;
            }
            destination[0] = new Range(0, 0);

            return 1;
        }
        var start = 0;
        var count = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != separator)
            {
                continue;
            }
            if (i > start || !removeEmptyEntries)
            {
                destination[count++] = new Range(start, i);
                if (count == destination.Length)
                {
                    return count;
                }
            }
            start = i + 1;
        }
        if (source.Length > start || !removeEmptyEntries)
        {
            destination[count++] = new Range(start, source.Length);
        }

        return count;
    }

    /// <summary>
    /// Counts the occurrences of <paramref name="value"/> in <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The text to search.</param>
    /// <param name="value">The character to count.</param>
    /// <returns>The number of occurrences.</returns>
    public static int Count(ReadOnlySpan<char> source, char value)
    {
        var count = 0;
        foreach (var c in source)
        {
            if (c == value)
            {
                count++;
            }
        }

        return count;
    }
}
