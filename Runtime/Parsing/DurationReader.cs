using System;
using System.Globalization;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Reads the duration brackets of holds (<c>1h[8:3]</c>) and slides (<c>1-5[8:1]</c>).
/// </summary>
internal static class DurationReader
{
    /// <summary>
    /// The culture used to read numbers.
    /// </summary>
    private static readonly CultureInfo s_culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets whether the first <c>]</c> of a hold comes before its first <c>[</c>. MajSimai 2.x failed on such a hold
    /// and dropped the rest of the timing point, so the caller must do the same.
    /// </summary>
    /// <param name="noteText">The note text without markers.</param>
    /// <returns><see langword="true"/> when both brackets exist and are in the wrong order.</returns>
    public static bool HasReversedHoldBrackets(ReadOnlySpan<char> noteText)
    {
        var start = noteText.IndexOf('[');
        var end = noteText.IndexOf(']');

        return start != -1 && end != -1 && end < start;
    }

    /// <summary>
    /// Reads the length of a hold from its first bracket: <c>[8:3]</c>, <c>[#1.5]</c> or <c>[150#8:3]</c>.
    /// </summary>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="noteText">The note text without markers; the brackets must not be reversed.</param>
    /// <param name="time">Receives the length in seconds, or 0 when it cannot be read.</param>
    /// <returns><see langword="true"/> when the length could be read.</returns>
    public static bool TryReadHoldTime(double bpm, ReadOnlySpan<char> noteText, out double time)
    {
        time = 0;
        var start = noteText.IndexOf('[');
        var end = noteText.IndexOf(']');
        if (start == -1 || end == -1)
        {
            return false;
        }
        var body = noteText.Slice(start + 1, end - start - 1);
        Span<Range> ranges = stackalloc Range[2];
        var count = LegacySplit.Split(body, ranges, '#', false);
        switch (count)
        {
            case 1:
                // 2h[8:3]
                return TryReadRatio(bpm, body, out time);
            case 2:
                {
                    var first = body[ranges[0]];
                    var second = body[ranges[1]];
                    if (first.IsEmpty)
                    {
                        // 2h[#5.678]
                        return TryParseDouble(second, out time);
                    }
                    // 2h[150#2:1]
                    if (second.IsEmpty || !TryParseDouble(first, out var customBpm))
                    {
                        time = 0;
                        return false;
                    }

                    return TryReadRatio(customBpm, second, out time);
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads the wait time and the length of a slide. Every bracket of a connected slide adds to the length; the wait
    /// time comes from the first bracket that sets one.
    /// </summary>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="noteText">The note text without markers.</param>
    /// <param name="waitTime">Receives the time in seconds between the star head and the start of the slide.</param>
    /// <param name="slideTime">Receives the slide length in seconds.</param>
    /// <returns><see langword="true"/> when the note has at least one bracket and every bracket could be read.</returns>
    public static bool TryReadSlideTimes(double bpm, ReadOnlySpan<char> noteText, out double waitTime, out double slideTime)
    {
        waitTime = 0;
        slideTime = 0;
        var totalTime = 0d;
        var customBpm = (double?)null;
        var nextStart = 0;
        var bracketIndex = noteText.IndexOf('[');
        if (bracketIndex == -1)
        {
            return false;
        }
        Span<Range> ranges = stackalloc Range[4];
        do
        {
            var slideBody = noteText.Slice(nextStart + bracketIndex);
            var closeIndex = slideBody.IndexOf(']');
            if (closeIndex == -1)
            {
                return false;
            }
            nextStart += closeIndex + bracketIndex + 1;
            var body = slideBody.Slice(1, closeIndex - 1);
            if (!TryReadSlideBracket(bpm, body, ranges, ref totalTime, ref customBpm))
            {
                return false;
            }
            bracketIndex = noteText.Slice(nextStart).IndexOf('[');
        }
        while (bracketIndex != -1);

        waitTime = 1d / ((customBpm ?? bpm) / 60d);
        slideTime = totalTime;

        return true;
    }

    /// <summary>
    /// Reads one slide bracket body and adds its length to <paramref name="totalTime"/>.
    /// </summary>
    private static bool TryReadSlideBracket(double bpm,
                                            ReadOnlySpan<char> body,
                                            Span<Range> ranges,
                                            ref double totalTime,
                                            ref double? customBpm)
    {
        var count = LegacySplit.Split(body, ranges, '#', false);
        switch (count)
        {
            case 1:
                {
                    // [8:1]
                    if (!TryReadRatio(bpm, body, out var time))
                    {
                        return false;
                    }
                    totalTime += time;

                    return true;
                }
            case 2:
                {
                    // [160#8:3] or [160#2]
                    var first = body[ranges[0]];
                    var second = body[ranges[1]];
                    if (first.IsEmpty || second.IsEmpty || !TryParseDouble(first, out var slideBpm))
                    {
                        return false;
                    }
                    if (TryParseDouble(second, out var time) || TryReadRatio(slideBpm, second, out time))
                    {
                        totalTime += time;
                    }
                    else
                    {
                        return false;
                    }
                    customBpm ??= slideBpm;

                    return true;
                }
            case 3:
                {
                    // [3##1.5] or [3##8:3]
                    var first = body[ranges[0]];
                    var third = body[ranges[2]];
                    if (first.IsEmpty || third.IsEmpty || !TryParseDouble(first, out var wait))
                    {
                        return false;
                    }
                    if (TryParseDouble(third, out var time) || TryReadRatio(bpm, third, out time))
                    {
                        totalTime += time;
                    }
                    else
                    {
                        return false;
                    }
                    customBpm ??= 60d / wait;

                    return true;
                }
            case 4:
                {
                    // [3##160#8:3]
                    var first = body[ranges[0]];
                    var third = body[ranges[2]];
                    var fourth = body[ranges[3]];
                    if (first.IsEmpty || third.IsEmpty || fourth.IsEmpty || !TryParseDouble(first, out var wait))
                    {
                        return false;
                    }
                    if (!TryParseDouble(third, out var slideBpm) || !TryReadRatio(slideBpm, fourth, out var time))
                    {
                        return false;
                    }
                    totalTime += time;
                    customBpm ??= 60d / wait;

                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads a <c>division:count</c> length, such as <c>8:3</c>, at a given BPM.
    /// </summary>
    /// <param name="bpm">The BPM the length is measured in.</param>
    /// <param name="text">The ratio text.</param>
    /// <param name="time">Receives the length in seconds.</param>
    /// <returns><see langword="true"/> when both numbers could be read.</returns>
    private static bool TryReadRatio(double bpm, ReadOnlySpan<char> text, out double time)
    {
        time = 0;
        var timeOneBeat = 1d / (bpm / 60d);
        Span<Range> ranges = stackalloc Range[2];
        if (LegacySplit.Split(text, ranges, ':', false) != 2)
        {
            return false;
        }
        var divisionText = text[ranges[0]];
        var countText = text[ranges[1]];
        if (divisionText.IsEmpty || countText.IsEmpty)
        {
            return false;
        }
        if (!TryParseInt(divisionText, out var division) || !TryParseInt(countText, out var count))
        {
            return false;
        }
        time = timeOneBeat * 4d / division * count;

        return true;
    }

    /// <summary>
    /// Parses an integer the way MajSimai 2.x did: <see cref="NumberStyles.Integer"/> with the invariant culture.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is an integer.</returns>
    public static bool TryParseInt(ReadOnlySpan<char> text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, s_culture, out value);
    }

    /// <summary>
    /// Parses a number the way MajSimai 2.x did: <see cref="NumberStyles.Float"/> and
    /// <see cref="NumberStyles.AllowThousands"/> with the invariant culture.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    public static bool TryParseDouble(ReadOnlySpan<char> text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, s_culture, out value);
    }

    /// <summary>
    /// Parses a single precision number the way MajSimai 2.x did: <see cref="NumberStyles.Float"/> and
    /// <see cref="NumberStyles.AllowThousands"/> with the invariant culture.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    public static bool TryParseFloat(ReadOnlySpan<char> text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, s_culture, out value);
    }
}
