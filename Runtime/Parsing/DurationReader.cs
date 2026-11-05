using System;
using System.Globalization;

namespace MajSimai.Parsing;

/// <summary>
/// The result of reading the duration of a hold or slide.
/// </summary>
internal enum DurationStatus
{
    /// <summary>The duration was read.</summary>
    Success,
    /// <summary>The note has no duration bracket.</summary>
    Missing,
    /// <summary>The bracket exists but its numbers cannot be read.</summary>
    Unreadable,
    /// <summary>The first <c>]</c> comes before the first <c>[</c>.</summary>
    ReversedBrackets,
    /// <summary>The bracket has more <c>:</c> or <c>#</c> separators than any duration form allows.</summary>
    TooManySeparators,
    /// <summary>A <c>[wait##length]</c> bracket has text between its two <c>#</c>.</summary>
    UnexpectedPart
}

/// <summary>
/// Reads the duration brackets of holds (<c>1h[8:3]</c>) and slides (<c>1-5[8:1]</c>), and the numbers of a chart.
/// </summary>
internal static class DurationReader
{
    /// <summary>
    /// The number styles of decimal numbers: <see cref="NumberStyles.Float"/>, which allows a sign, a decimal point and
    /// an exponent. Thousands separators are not allowed, so <c>0,071</c> is not read as 71.
    /// </summary>
    private const NumberStyles DecimalStyle = NumberStyles.Float;

    /// <summary>
    /// The culture used to read numbers.
    /// </summary>
    private static readonly CultureInfo s_culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets whether a status means the duration is malformed in a way that is always an error.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> for reversed brackets, too many separators and unexpected parts.</returns>
    public static bool IsMalformed(DurationStatus status)
    {
        return status is DurationStatus.ReversedBrackets or DurationStatus.TooManySeparators or DurationStatus.UnexpectedPart;
    }

    /// <summary>
    /// Reads the length of a hold from its first bracket: <c>[8:3]</c>, <c>[#1.5]</c> or <c>[150#8:3]</c>.
    /// </summary>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="noteText">The note text without markers.</param>
    /// <param name="time">Receives the length in seconds, or 0 when it cannot be read.</param>
    /// <returns>Whether the length was read, and why not.</returns>
    public static DurationStatus ReadHoldTime(double bpm, ReadOnlySpan<char> noteText, out double time)
    {
        time = 0;
        var start = noteText.IndexOf('[');
        var end = noteText.IndexOf(']');
        if (start == -1 || end == -1)
        {
            return DurationStatus.Missing;
        }
        if (end < start)
        {
            return DurationStatus.ReversedBrackets;
        }
        var body = noteText.Slice(start + 1, end - start - 1);
        var hashCount = Count(body, '#');
        if (hashCount > 1)
        {
            return DurationStatus.TooManySeparators;
        }
        if (hashCount == 0)
        {
            // 2h[8:3]
            return ReadRatio(bpm, body, out time);
        }
        var hash = body.IndexOf('#');
        var first = body.Slice(0, hash);
        var second = body.Slice(hash + 1);
        if (first.IsEmpty)
        {
            // 2h[#5.678]
            return TryParseDouble(second, out time) ? DurationStatus.Success : DurationStatus.Unreadable;
        }
        // 2h[150#2:1]
        if (second.IsEmpty || !TryParseDouble(first, out var customBpm))
        {
            return DurationStatus.Unreadable;
        }

        return ReadRatio(customBpm, second, out time);
    }

    /// <summary>
    /// Reads the wait time and the length of a slide. Every bracket of a connected slide adds to the length; the wait
    /// time comes from the first bracket that sets one.
    /// </summary>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="noteText">The note text without markers.</param>
    /// <param name="waitTime">Receives the time in seconds between the star head and the start of the slide.</param>
    /// <param name="slideTime">Receives the slide length in seconds.</param>
    /// <returns>Whether the times were read, and why not.</returns>
    public static DurationStatus ReadSlideTimes(double bpm, ReadOnlySpan<char> noteText, out double waitTime, out double slideTime)
    {
        waitTime = 0;
        slideTime = 0;
        var totalTime = 0d;
        var customBpm = (double?)null;
        var nextStart = 0;
        var bracketIndex = noteText.IndexOf('[');
        if (bracketIndex == -1)
        {
            return DurationStatus.Missing;
        }
        do
        {
            var slideBody = noteText.Slice(nextStart + bracketIndex);
            var closeIndex = slideBody.IndexOf(']');
            if (closeIndex == -1)
            {
                return DurationStatus.Unreadable;
            }
            nextStart += closeIndex + bracketIndex + 1;
            var status = ReadSlideBracket(bpm, slideBody.Slice(1, closeIndex - 1), ref totalTime, ref customBpm);
            if (status != DurationStatus.Success)
            {
                return status;
            }
            bracketIndex = noteText.Slice(nextStart).IndexOf('[');
        }
        while (bracketIndex != -1);

        waitTime = 1d / ((customBpm ?? bpm) / 60d);
        slideTime = totalTime;

        return DurationStatus.Success;
    }

    /// <summary>
    /// Reads one slide bracket body and adds its length to <paramref name="totalTime"/>.
    /// </summary>
    /// <param name="bpm">The BPM in effect.</param>
    /// <param name="body">The text between the brackets.</param>
    /// <param name="totalTime">The slide length so far.</param>
    /// <param name="customBpm">The BPM that sets the wait time, once a bracket sets one.</param>
    /// <returns>Whether the bracket was read, and why not.</returns>
    private static DurationStatus ReadSlideBracket(double bpm, ReadOnlySpan<char> body, ref double totalTime, ref double? customBpm)
    {
        var hashCount = Count(body, '#');
        if (hashCount > 3)
        {
            return DurationStatus.TooManySeparators;
        }
        Span<Range> parts = stackalloc Range[4];
        Split(body, '#', parts);
        double time;
        DurationStatus status;
        switch (hashCount)
        {
            case 0:
                // [8:1]
                status = ReadRatio(bpm, body, out time);
                if (status != DurationStatus.Success)
                {
                    return status;
                }
                totalTime += time;

                return DurationStatus.Success;
            case 1:
                {
                    // [160#8:3] or [160#2]
                    var first = body[parts[0]];
                    var second = body[parts[1]];
                    if (first.IsEmpty || second.IsEmpty || !TryParseDouble(first, out var slideBpm))
                    {
                        return DurationStatus.Unreadable;
                    }
                    status = TryParseDouble(second, out time) ? DurationStatus.Success : ReadRatio(slideBpm, second, out time);
                    if (status != DurationStatus.Success)
                    {
                        return status;
                    }
                    totalTime += time;
                    customBpm ??= slideBpm;

                    return DurationStatus.Success;
                }
            case 2:
                {
                    // [3##1.5] or [3##8:3]
                    if (!body[parts[1]].IsEmpty)
                    {
                        return DurationStatus.UnexpectedPart;
                    }
                    var first = body[parts[0]];
                    var third = body[parts[2]];
                    if (first.IsEmpty || third.IsEmpty || !TryParseDouble(first, out var wait))
                    {
                        return DurationStatus.Unreadable;
                    }
                    status = TryParseDouble(third, out time) ? DurationStatus.Success : ReadRatio(bpm, third, out time);
                    if (status != DurationStatus.Success)
                    {
                        return status;
                    }
                    totalTime += time;
                    customBpm ??= 60d / wait;

                    return DurationStatus.Success;
                }
            default:
                {
                    // [3##160#8:3]
                    if (!body[parts[1]].IsEmpty)
                    {
                        return DurationStatus.UnexpectedPart;
                    }
                    var first = body[parts[0]];
                    var third = body[parts[2]];
                    var fourth = body[parts[3]];
                    if (first.IsEmpty || third.IsEmpty || fourth.IsEmpty || !TryParseDouble(first, out var wait))
                    {
                        return DurationStatus.Unreadable;
                    }
                    if (!TryParseDouble(third, out var slideBpm))
                    {
                        return DurationStatus.Unreadable;
                    }
                    status = ReadRatio(slideBpm, fourth, out time);
                    if (status != DurationStatus.Success)
                    {
                        return status;
                    }
                    totalTime += time;
                    customBpm ??= 60d / wait;

                    return DurationStatus.Success;
                }
        }
    }

    /// <summary>
    /// Reads a <c>division:count</c> length, such as <c>8:3</c>, at a given BPM.
    /// </summary>
    /// <param name="bpm">The BPM the length is measured in.</param>
    /// <param name="text">The ratio text.</param>
    /// <param name="time">Receives the length in seconds.</param>
    /// <returns>Whether the length was read, and why not.</returns>
    private static DurationStatus ReadRatio(double bpm, ReadOnlySpan<char> text, out double time)
    {
        time = 0;
        var colonCount = Count(text, ':');
        if (colonCount > 1)
        {
            return DurationStatus.TooManySeparators;
        }
        if (colonCount == 0)
        {
            return DurationStatus.Unreadable;
        }
        var colon = text.IndexOf(':');
        var divisionText = text.Slice(0, colon);
        var countText = text.Slice(colon + 1);
        if (divisionText.IsEmpty || countText.IsEmpty)
        {
            return DurationStatus.Unreadable;
        }
        if (!TryParseInt(divisionText, out var division) || !TryParseInt(countText, out var count))
        {
            return DurationStatus.Unreadable;
        }
        var timeOneBeat = 1d / (bpm / 60d);
        time = timeOneBeat * 4d / division * count;

        return DurationStatus.Success;
    }

    /// <summary>
    /// Splits <paramref name="text"/> at every <paramref name="separator"/>. The caller makes sure there is room for
    /// every part.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="separator">The separator.</param>
    /// <param name="parts">Receives the range of each part.</param>
    private static void Split(ReadOnlySpan<char> text, char separator, Span<Range> parts)
    {
        var start = 0;
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == separator)
            {
                parts[count++] = new Range(start, i);
                start = i + 1;
            }
        }
        parts[count] = new Range(start, text.Length);
    }

    /// <summary>
    /// Counts the occurrences of a character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The character.</param>
    /// <returns>The number of occurrences.</returns>
    private static int Count(ReadOnlySpan<char> text, char value)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == value)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Parses an integer: an optional sign and digits, with the invariant culture.
    /// </summary>
    /// <remarks>
    /// Durations such as <c>[8:1]</c> are plain digits almost always, so up to nine of them are added up directly; that
    /// gives the value <see cref="int.TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider, out int)"/> would
    /// give and cannot overflow. Any other text is left to it.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is an integer.</returns>
    public static bool TryParseInt(ReadOnlySpan<char> text, out int value)
    {
        if (TryParseDigits(text, out value))
        {
            return true;
        }

        return int.TryParse(text, NumberStyles.Integer, s_culture, out value);
    }

    /// <summary>
    /// Reads one to nine ASCII digits.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value, or 0 when the text is not one to nine digits.</param>
    /// <returns><see langword="true"/> when the text is one to nine digits.</returns>
    private static bool TryParseDigits(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (text.IsEmpty || text.Length > 9)
        {
            return false;
        }
        var result = 0;
        foreach (var c in text)
        {
            var digit = c - '0';
            if ((uint)digit > 9)
            {
                return false;
            }
            result = result * 10 + digit;
        }
        value = result;

        return true;
    }

    /// <summary>
    /// Parses a decimal number with <see cref="NumberStyles.Float"/> and the invariant culture. Thousands separators are
    /// rejected; <c>NaN</c> and <c>Infinity</c> are accepted as MajSimai 2.x did, because charts use them as effects.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    public static bool TryParseDouble(ReadOnlySpan<char> text, out double value)
    {
        return double.TryParse(text, DecimalStyle, s_culture, out value);
    }

    /// <summary>
    /// Parses a single precision decimal number with the same rules as <see cref="TryParseDouble"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    public static bool TryParseFloat(ReadOnlySpan<char> text, out float value)
    {
        return float.TryParse(text, DecimalStyle, s_culture, out value);
    }
}
