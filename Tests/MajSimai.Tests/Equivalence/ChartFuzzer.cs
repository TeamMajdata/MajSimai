using System;
using System.Text;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Generates random chart texts that mix valid simai with the malformed input the parser must handle the same way as
/// MajSimai 2.x.
/// </summary>
internal sealed class ChartFuzzer
{
    /// <summary>
    /// The slide shapes.
    /// </summary>
    private static readonly string[] s_slideMarks = { "-", "^", "v", "<", ">", "V", "p", "q", "s", "z", "w", "pp", "qq", "K" };

    /// <summary>
    /// The note markers.
    /// </summary>
    private static readonly string[] s_markers = { "b", "x", "m", "$", "$$", "@", "!", "?", "c", "f", "h" };

    /// <summary>
    /// Valid and malformed duration brackets.
    /// </summary>
    private static readonly string[] s_durations =
    {
        "[8:1]", "[4:3]", "[16:5]", "[160#8:3]", "[160#2]", "[#1.5]", "[3##1.5]", "[3##8:3]", "[3##160#8:3]",
        "[1#2#3#4#5]", "[8:1:2]", "[]", "[:]", "[0:1]", "[8:]", "[a:b]", "[#]", "[##]", "[1,000#4:1]", "[ 8:1 ]",
        "[-8:1]", "[+4:1]", "[1e1#4:1]", "[8:1", "]8:1[", "[8:1]]", "[[8:1]",
    };

    /// <summary>
    /// Valid declarations and comments.
    /// </summary>
    private static readonly string[] s_declarations =
    {
        "(120)", "(180.5)", "(60)", "{4}", "{8}", "{16}", "{#0.25}", "{3}", "<HS*1.5>", "<SV*0.5>", "<HS*2>",
        "||comment\n", "||s3/4\n", "||s7/8\n", "||s3/4/5\n", "||sx/y\n", "|| s3/4\n", "||\n",
    };

    /// <summary>
    /// Malformed declarations that fail the chart.
    /// </summary>
    private static readonly string[] s_badDeclarations =
    {
        "(abc)", "()", "{}", "{#x}", "{x}", "<HS*x>", "<SV*>", "<XX*1>", "<HS*1*2>", "<HS>", "|x", "(", "{", "<", "||",
    };

    /// <summary>
    /// Characters that may appear anywhere.
    /// </summary>
    private static readonly string[] s_noise = { " ", "\r", "\n", "\t", "E", ")", "}", ">", "#", "&", "　" };

    /// <summary>
    /// The random generator.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// Creates a fuzzer.
    /// </summary>
    /// <param name="seed">The seed of the random generator, so failures can be reproduced.</param>
    public ChartFuzzer(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Generates a chart text.
    /// </summary>
    /// <param name="timingPoints">The approximate number of timing points.</param>
    /// <param name="malformedChance">The chance that a declaration is malformed, which usually fails the chart.</param>
    /// <returns>The chart text.</returns>
    public string NextChart(int timingPoints, double malformedChance = 0.002)
    {
        var builder = new StringBuilder();
        builder.Append('(').Append(Pick(new[] { "120", "150", "200.5", "97" })).Append(')');
        builder.Append('{').Append(Pick(new[] { "4", "8", "16", "1" })).Append('}');
        for (var i = 0; i < timingPoints; i++)
        {
            if (Chance(0.08))
            {
                builder.Append(Chance(malformedChance / 0.08) ? Pick(s_badDeclarations) : Pick(s_declarations));
            }
            if (!Chance(0.15))
            {
                AppendTimingPoint(builder);
            }
            if (Chance(0.03))
            {
                builder.Append(Pick(s_noise));
            }
            builder.Append(',');
            if (Chance(0.2))
            {
                builder.Append('\n');
            }
        }
        if (Chance(0.3))
        {
            AppendTimingPoint(builder);
        }
        if (Chance(0.2))
        {
            builder.Append('E');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generates the text of one timing point.
    /// </summary>
    private void AppendTimingPoint(StringBuilder builder)
    {
        if (Chance(0.05))
        {
            // Two digits: two taps.
            builder.Append(_random.Next(10)).Append(_random.Next(10));
            return;
        }
        var groups = Chance(0.6) ? 1 : _random.Next(2, 5);
        var separator = Chance(0.1) ? '`' : '/';
        for (var g = 0; g < groups; g++)
        {
            if (g > 0)
            {
                builder.Append(Chance(0.9) ? separator : Pick(new[] { '/', '`', '*' }));
            }
            AppendGroup(builder);
        }
        if (Chance(0.02))
        {
            builder.Append(Chance(0.5) ? "/" : "`");
        }
    }

    /// <summary>
    /// Appends one <c>/</c> separated group: a slide, a touch or a button note.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendGroup(StringBuilder builder)
    {
        if (Chance(0.45))
        {
            AppendSlide(builder);
            var parts = Chance(0.75) ? 0 : _random.Next(1, 4);
            for (var i = 0; i < parts; i++)
            {
                builder.Append('*');
                if (Chance(0.9))
                {
                    AppendSlidePath(builder);
                }
                else if (Chance(0.5))
                {
                    AppendMarkers(builder);
                }
            }
            if (Chance(0.03))
            {
                builder.Append('*');
            }
            return;
        }
        if (Chance(0.3))
        {
            AppendTouch(builder);
            return;
        }
        AppendButtonNote(builder);
    }

    /// <summary>
    /// Appends a tap or hold.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendButtonNote(StringBuilder builder)
    {
        builder.Append(Chance(0.97) ? (char)('1' + _random.Next(8)) : Pick(new[] { '0', '9', 'x', 'b' }));
        AppendMarkers(builder);
        if (Chance(0.2))
        {
            builder.Append('h');
            AppendMarkers(builder);
            if (Chance(0.8))
            {
                builder.Append(Pick(s_durations));
            }
            AppendMarkers(builder);
        }
    }

    /// <summary>
    /// Appends a touch or touch hold.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendTouch(StringBuilder builder)
    {
        var area = (char)('A' + _random.Next(5));
        builder.Append(area);
        if (area != 'C' || Chance(0.3))
        {
            builder.Append(Chance(0.95) ? (char)('1' + _random.Next(8)) : 'x');
        }
        AppendMarkers(builder);
        if (Chance(0.25))
        {
            builder.Append('h');
            AppendMarkers(builder);
            if (Chance(0.8))
            {
                builder.Append(Pick(s_durations));
            }
        }
    }

    /// <summary>
    /// Appends a slide with its head button.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendSlide(StringBuilder builder)
    {
        builder.Append((char)('1' + _random.Next(8)));
        AppendMarkers(builder);
        AppendSlidePath(builder);
    }

    /// <summary>
    /// Appends a slide path without its head button.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendSlidePath(StringBuilder builder)
    {
        var segments = Chance(0.8) ? 1 : _random.Next(2, 4);
        for (var i = 0; i < segments; i++)
        {
            builder.Append(Pick(s_slideMarks));
            if (Chance(0.1))
            {
                AppendMarkers(builder);
            }
            builder.Append((char)('1' + _random.Next(8)));
            if (Chance(0.15))
            {
                AppendMarkers(builder);
            }
            if (i == segments - 1 || Chance(0.4))
            {
                if (Chance(0.95))
                {
                    builder.Append(Pick(s_durations));
                }
            }
        }
        if (Chance(0.2))
        {
            AppendMarkers(builder);
        }
    }

    /// <summary>
    /// Appends random markers.
    /// </summary>
    /// <param name="builder">The chart text.</param>
    private void AppendMarkers(StringBuilder builder)
    {
        while (Chance(0.2))
        {
            builder.Append(Pick(s_markers));
        }
        if (Chance(0.01))
        {
            builder.Append(Pick(s_noise));
        }
    }

    /// <summary>
    /// Draws a random event.
    /// </summary>
    /// <param name="probability">The chance of the event.</param>
    /// <returns><see langword="true"/> when the event happens.</returns>
    private bool Chance(double probability)
    {
        return _random.NextDouble() < probability;
    }

    /// <summary>
    /// Picks a random value.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>One of the values.</returns>
    private T Pick<T>(T[] values)
    {
        return values[_random.Next(values.Length)];
    }
}
