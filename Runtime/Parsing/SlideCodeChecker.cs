using System;

namespace MajSimai.Parsing;

/// <summary>
/// Checks a slide segment written in Slide Code, such as <c>A357K1</c> in <c>1A357K1[2:1]</c>, following the MajSimai
/// documentation and the MajdataPlay implementation.
/// </summary>
/// <remarks>
/// <para>
/// Syntax: the commands are <c>A</c>, <c>B</c> (go to that area, 1 to 8), <c>C</c> (go to the center, no parameter),
/// <c>P</c>, <c>Q</c> (turn counterclockwise or clockwise around circle 0 to 9) and <c>K</c> (end at a button, 1 to 8).
/// A digit without a letter repeats the previous command. The segment must end with <c>K</c> and its button.
/// </para>
/// <para>
/// Connections: a node may not go to or leave a circle that contains it (the center is inside circle 0, the B nodes
/// near circle N are inside it, and every B and C node is inside circle 9); <c>P</c> cannot follow <c>Q</c> directly or
/// the other way round; circles 0 and 9 cannot follow each other.
/// </para>
/// <para>
/// Warnings: going to the point the path is already at, and going around the same circle three or more times in a row,
/// which players cannot read.
/// </para>
/// </remarks>
internal static class SlideCodeChecker
{
    /// <summary>
    /// The radius of the judgment line, which holds the A nodes and is circle 9.
    /// </summary>
    private const double MainRadius = 4.80;

    /// <summary>
    /// How far a node may be from a circle and still count as on it.
    /// </summary>
    private const double OnCircleTolerance = 0.1;

    /// <summary>
    /// How close two nodes must be to count as the same point.
    /// </summary>
    private const double SamePointTolerance = 1e-6;

    /// <summary>
    /// The number of identical circle commands in a row that gets a warning.
    /// </summary>
    private const int ManyTurnsCount = 3;

    /// <summary>
    /// The radius of the center circle (circle 0).
    /// </summary>
    private static readonly double s_centerRadius = MainRadius * Math.Cos(Math.PI * 3 / 8);

    /// <summary>
    /// The distance of the B nodes from the center.
    /// </summary>
    private static readonly double s_groupBRadius = s_centerRadius / Math.Cos(Math.PI / 8);

    /// <summary>
    /// The radius of the side circles (circles 1 to 8), which is also the distance of their centers from the center.
    /// </summary>
    private static readonly double s_sideRadius = MainRadius * Math.Cos(Math.PI / 8) / 2;

    /// <summary>
    /// The kind of a Slide Code command.
    /// </summary>
    private enum CommandKind
    {
        /// <summary>A node on the judgment line: the start button, <c>A</c> or <c>K</c>.</summary>
        NodeA,
        /// <summary>A <c>B</c> node.</summary>
        NodeB,
        /// <summary>The <c>C</c> node.</summary>
        NodeC,
        /// <summary><c>P</c>: turn counterclockwise.</summary>
        TurnP,
        /// <summary><c>Q</c>: turn clockwise.</summary>
        TurnQ
    }

    /// <summary>
    /// Gets whether a character is a Slide Code command.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for <c>A</c>, <c>B</c>, <c>C</c>, <c>K</c>, <c>P</c> and <c>Q</c>.</returns>
    public static bool IsCommand(char c)
    {
        return c is 'A' or 'B' or 'C' or 'K' or 'P' or 'Q';
    }

    /// <summary>
    /// Reads and checks a Slide Code segment.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="text">The slide text without markers.</param>
    /// <param name="index">The index of the first command; receives the index after the end button.</param>
    /// <param name="start">The start button of the segment.</param>
    /// <param name="noteText">The note text with markers, for messages.</param>
    /// <param name="end">Receives the end button of the segment.</param>
    /// <returns><see langword="true"/> when the segment can be kept; <see langword="false"/> when an error was reported.</returns>
    /// <exception cref="InvalidSimaiNoteException">Thrown when the segment is invalid and the reporter does not collect.</exception>
    public static bool Check(in NoteSource source, ReadOnlySpan<char> text, ref int index, int start, ReadOnlySpan<char> noteText, out int end)
    {
        end = 0;
        var commandStart = index;
        var segmentEnd = -1;
        for (var i = index; i < text.Length; i++)
        {
            var c = text[i];
            if (c == 'K' && i + 1 < text.Length && IsDigit(text[i + 1]))
            {
                segmentEnd = i + 2;
                break;
            }
            if (!IsCommand(c) && !IsDigit(c))
            {
                return Fail(source, SyntaxRules.InvalidSlideCode, noteText, $"has '{c}' inside the Slide Code \"{text.Slice(commandStart, i - commandStart).ToString()}\"");
            }
        }
        if (segmentEnd == -1)
        {
            return Fail(source, SyntaxRules.InvalidSlideCode, noteText, "has Slide Code that does not end with 'K' and an end button");
        }
        index = segmentEnd;
        var code = text.Slice(commandStart, segmentEnd - commandStart);
        if (segmentEnd < text.Length && IsDigit(text[segmentEnd]))
        {
            return Fail(source, SyntaxRules.InvalidSlideCode, noteText, $"has a digit after the end of the Slide Code \"{code.ToString()}\"; 'K' must be the last command");
        }

        var previousKind = CommandKind.NodeA;
        var previousValue = start;
        var kind = CommandKind.NodeA;
        var letter = '\0';
        var hasParameter = true;
        var repeatCount = 1;
        var isValid = true;
        for (var i = 0; i < code.Length && isValid; i++)
        {
            var c = code[i];
            if (IsCommand(c))
            {
                if (!hasParameter)
                {
                    return Fail(source, SyntaxRules.InvalidSlideCode, noteText, $"has the Slide Code command '{letter}' without a parameter in \"{code.ToString()}\"");
                }
                letter = c;
                kind = ToKind(c);
                hasParameter = c == 'C';
                if (c == 'C')
                {
                    isValid = Connect(source, noteText, code, previousKind, previousValue, kind, 0, ref repeatCount);
                    previousKind = kind;
                    previousValue = 0;
                }
                continue;
            }
            var value = c - '0';
            if (letter == 'C')
            {
                return Fail(source, SyntaxRules.InvalidSlideCode, noteText, $"has a digit after 'C' in the Slide Code \"{code.ToString()}\"; 'C' takes no parameter");
            }
            var isTurn = kind is CommandKind.TurnP or CommandKind.TurnQ;
            if (!isTurn && (value < 1 || value > 8))
            {
                return Fail(source, SyntaxRules.InvalidSlideCode, noteText, $"has '{letter}{c}' in the Slide Code \"{code.ToString()}\"; '{letter}' takes a button from 1 to 8");
            }
            hasParameter = true;
            isValid = Connect(source, noteText, code, previousKind, previousValue, kind, value, ref repeatCount);
            previousKind = kind;
            previousValue = value;
        }
        end = previousValue;

        return isValid;
    }

    /// <summary>
    /// Checks the connection between two commands.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="noteText">The note text, for messages.</param>
    /// <param name="code">The Slide Code segment, for messages.</param>
    /// <param name="fromKind">The kind of the previous command.</param>
    /// <param name="fromValue">The parameter of the previous command.</param>
    /// <param name="toKind">The kind of the command.</param>
    /// <param name="toValue">The parameter of the command.</param>
    /// <param name="repeatCount">The number of identical circle commands in a row so far.</param>
    /// <returns><see langword="true"/> when the connection is possible.</returns>
    private static bool Connect(in NoteSource source,
                                ReadOnlySpan<char> noteText,
                                ReadOnlySpan<char> code,
                                CommandKind fromKind,
                                int fromValue,
                                CommandKind toKind,
                                int toValue,
                                ref int repeatCount)
    {
        var fromTurn = fromKind is CommandKind.TurnP or CommandKind.TurnQ;
        var toTurn = toKind is CommandKind.TurnP or CommandKind.TurnQ;
        var connection = $"{Name(fromKind, fromValue)} to {Name(toKind, toValue)}";
        if (!fromTurn && !toTurn)
        {
            repeatCount = 1;
            if (Distance(GetNode(fromKind, fromValue), GetNode(toKind, toValue)) < SamePointTolerance)
            {
                source.Report(SyntaxRules.ZeroLengthSlideCode, $"The Slide Code \"{code.ToString()}\" in \"{noteText.ToString()}\" goes from {connection}, which is the same point.", noteText);
            }
            return true;
        }
        if (fromTurn != toTurn)
        {
            repeatCount = 1;
            var node = fromTurn ? GetNode(toKind, toValue) : GetNode(fromKind, fromValue);
            var circle = fromTurn ? fromValue : toValue;
            if (IsInside(node, circle))
            {
                return Fail(source, SyntaxRules.ImpossibleSlideCodeConnection, noteText, $"has Slide Code \"{code.ToString()}\" that goes from {connection}, but the node is inside the circle");
            }
            return true;
        }
        if (fromKind != toKind)
        {
            return Fail(source, SyntaxRules.ImpossibleSlideCodeConnection, noteText, $"has Slide Code \"{code.ToString()}\" that goes from {connection}; 'P' and 'Q' need a node between them");
        }
        if ((fromValue == 0 && toValue == 9) || (fromValue == 9 && toValue == 0))
        {
            return Fail(source, SyntaxRules.ImpossibleSlideCodeConnection, noteText, $"has Slide Code \"{code.ToString()}\" that goes from {connection}; circles 0 and 9 cannot follow each other");
        }
        repeatCount = fromValue == toValue ? repeatCount + 1 : 1;
        if (repeatCount == ManyTurnsCount)
        {
            source.Report(SyntaxRules.ManyTurnsSlideCode, $"The Slide Code \"{code.ToString()}\" in \"{noteText.ToString()}\" goes around circle {toValue} {ManyTurnsCount} or more times in a row, which is hard to read.", noteText);
        }

        return true;
    }

    /// <summary>
    /// Reports an invalid Slide Code segment.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="rule">The broken rule.</param>
    /// <param name="noteText">The note text.</param>
    /// <param name="problem">The problem, completing "The slide ... ".</param>
    /// <returns>Always <see langword="false"/>.</returns>
    private static bool Fail(in NoteSource source, SyntaxRule rule, ReadOnlySpan<char> noteText, string problem)
    {
        source.Report(rule, $"The slide \"{noteText.ToString()}\" {problem}.", noteText);

        return false;
    }

    /// <summary>
    /// Gets whether a node is strictly inside a circle; a node on the circle is not.
    /// </summary>
    /// <param name="node">The node position.</param>
    /// <param name="circle">The circle number, 0 to 9.</param>
    /// <returns><see langword="true"/> when the node is inside.</returns>
    private static bool IsInside((double X, double Y) node, int circle)
    {
        double centerX = 0;
        double centerY = 0;
        double radius;
        switch (circle)
        {
            case 0:
                radius = s_centerRadius;
                break;
            case 9:
                radius = MainRadius;
                break;
            default:
                var angle = Math.PI * (3.0 / 4.0 - circle / 4.0);
                centerX = s_sideRadius * Math.Cos(angle);
                centerY = s_sideRadius * Math.Sin(angle);
                radius = s_sideRadius;
                break;
        }
        var distance = Distance(node, (centerX, centerY));
        if (Math.Abs(distance - radius) < OnCircleTolerance)
        {
            return false;
        }

        return distance < radius;
    }

    /// <summary>
    /// Gets the position of a node.
    /// </summary>
    /// <param name="kind">The kind of the node.</param>
    /// <param name="value">The area number, 1 to 8.</param>
    /// <returns>The position.</returns>
    private static (double X, double Y) GetNode(CommandKind kind, int value)
    {
        if (kind == CommandKind.NodeC)
        {
            return (0, 0);
        }
        var radius = kind == CommandKind.NodeA ? MainRadius : s_groupBRadius;
        var angle = Math.PI * (5.0 / 8.0 - value / 4.0);

        return (radius * Math.Cos(angle), radius * Math.Sin(angle));
    }

    /// <summary>
    /// Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Gets the kind of a command letter.
    /// </summary>
    /// <param name="c">The letter.</param>
    /// <returns>The kind.</returns>
    private static CommandKind ToKind(char c)
    {
        return c switch
        {
            'B' => CommandKind.NodeB,
            'C' => CommandKind.NodeC,
            'P' => CommandKind.TurnP,
            'Q' => CommandKind.TurnQ,
            _ => CommandKind.NodeA,
        };
    }

    /// <summary>
    /// Names a command for messages.
    /// </summary>
    /// <param name="kind">The kind of the command.</param>
    /// <param name="value">The parameter.</param>
    /// <returns>A name such as <c>B3</c> or <c>circle 5</c>.</returns>
    private static string Name(CommandKind kind, int value)
    {
        return kind switch
        {
            CommandKind.NodeA => $"A{value}",
            CommandKind.NodeB => $"B{value}",
            CommandKind.NodeC => "C",
            CommandKind.TurnP => $"P{value}",
            _ => $"Q{value}",
        };
    }

    /// <summary>
    /// Gets whether a character is a decimal digit.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for 0 to 9.</returns>
    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }
}
