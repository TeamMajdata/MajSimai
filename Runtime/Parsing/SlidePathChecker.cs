using System;

namespace MajSimai.Parsing;

/// <summary>
/// Checks the path of a slide in strict mode and in the syntax checker: every segment has a known shape and an end button
/// that the shape allows, durations are on every segment or only on the last one, and <c>w</c> is not connected.
/// </summary>
/// <remarks>
/// The end buttons each shape allows follow the official simai table (全SLIDEの始点・終点の関係表), measured as the
/// distance from the start button going clockwise (0 is the start button itself, 4 the opposite one):
/// <list type="bullet">
/// <item><c>-</c>: 2 to 6.</item>
/// <item><c>^</c>: any but 0 and 4.</item>
/// <item><c>v</c>: any but 4. 0 (<c>1v1</c>) is a Majdata extension and only gets a warning.</item>
/// <item><c>&lt;</c>, <c>&gt;</c>, <c>p</c>, <c>q</c>, <c>pp</c>, <c>qq</c>: any.</item>
/// <item><c>s</c>, <c>z</c>, <c>w</c>: only 4.</item>
/// <item><c>V</c>: the turn button is 2 away; turning counterclockwise the end is 1 to 4 away from the start, turning
/// clockwise it is 4 to 7 away.</item>
/// </list>
/// Segments written in Slide Code are checked by <see cref="SlideCodeChecker"/>.
/// </remarks>
internal static class SlidePathChecker
{
    /// <summary>
    /// Checks a slide path.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="text">The slide text without markers, starting with a button from 1 to 8.</param>
    /// <param name="noteText">The note text with markers, for messages.</param>
    /// <returns><see langword="true"/> when the slide can be kept; <see langword="false"/> when an error was reported.</returns>
    /// <exception cref="InvalidSimaiNoteException">Thrown when the path is invalid and the reporter does not collect.</exception>
    public static bool Check(in NoteSource source, ReadOnlySpan<char> text, ReadOnlySpan<char> noteText)
    {
        var start = text[0] - '0';
        var index = 1;
        var segmentCount = 0;
        var durationCount = 0;
        var lastHasDuration = false;
        var hasWifi = false;
        while (index < text.Length)
        {
            var c = text[index];
            int end;
            if (SlideCodeChecker.IsCommand(c))
            {
                if (!SlideCodeChecker.Check(source, text, ref index, start, noteText, out end))
                {
                    return false;
                }
            }
            else if (!CheckShape(source, text, ref index, start, noteText, out end, ref hasWifi))
            {
                return false;
            }
            segmentCount++;
            start = end;
            lastHasDuration = index < text.Length && text[index] == '[';
            if (lastHasDuration)
            {
                durationCount++;
                index += text.Slice(index).IndexOf(']') + 1;
            }
        }
        var noteString = noteText.ToString();
        if (segmentCount == 0)
        {
            source.Report(SyntaxRules.InvalidSlidePath, $"The slide \"{noteString}\" has no shape.", noteText);
            return false;
        }
        if (!lastHasDuration || (durationCount != segmentCount && durationCount != 1))
        {
            source.Report(SyntaxRules.InconsistentSlideDuration,
                          $"The slide \"{noteString}\" must have a duration after every segment, or only after the last one.",
                          noteText);
            return false;
        }
        if (hasWifi && segmentCount > 1)
        {
            source.Report(SyntaxRules.WifiInConnectedSlide, $"The 'w' slide in \"{noteString}\" cannot be part of a connected slide.", noteText);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads and checks one segment written with a simai shape.
    /// </summary>
    /// <param name="source">The timing point.</param>
    /// <param name="text">The slide text without markers.</param>
    /// <param name="index">The index of the shape; receives the index after the end button.</param>
    /// <param name="start">The start button of the segment.</param>
    /// <param name="noteText">The note text with markers, for messages.</param>
    /// <param name="end">Receives the end button of the segment.</param>
    /// <param name="hasWifi">Set to <see langword="true"/> when the segment is a <c>w</c> slide.</param>
    /// <returns><see langword="true"/> when the segment can be kept.</returns>
    private static bool CheckShape(in NoteSource source,
                                   ReadOnlySpan<char> text,
                                   ref int index,
                                   int start,
                                   ReadOnlySpan<char> noteText,
                                   out int end,
                                   ref bool hasWifi)
    {
        end = 0;
        var shape = text[index];
        if (shape is not ('-' or '^' or 'v' or '<' or '>' or 'p' or 'q' or 's' or 'z' or 'w' or 'V'))
        {
            source.Report(SyntaxRules.InvalidSlidePath, $"The slide \"{noteText.ToString()}\" has an unexpected '{shape}' where a shape is expected.", noteText);
            return false;
        }
        var shapeText = shape.ToString();
        index++;
        if ((shape == 'p' || shape == 'q') && index < text.Length && text[index] == shape)
        {
            shapeText += shape;
            index++;
        }
        var turn = 0;
        if (shape == 'V' && !TryReadButton(text, ref index, out turn))
        {
            source.Report(SyntaxRules.InvalidSlidePath, $"The 'V' slide in \"{noteText.ToString()}\" needs a turn button from 1 to 8.", noteText);
            return false;
        }
        if (!TryReadButton(text, ref index, out end))
        {
            source.Report(SyntaxRules.InvalidSlidePath, $"The '{shapeText}' slide in \"{noteText.ToString()}\" needs an end button from 1 to 8.", noteText);
            return false;
        }
        hasWifi |= shape == 'w';
        var distance = Distance(start, end);
        var segment = shape == 'V' ? $"{start}V{turn}{end}" : $"{start}{shapeText}{end}";
        if (shape == 'v' && distance == 0)
        {
            source.Report(SyntaxRules.MajdataOnlySlideEnd, $"The slide \"{segment}\" in \"{noteText.ToString()}\" back to its start button is a Majdata extension that official simai does not define.", noteText);
            return true;
        }
        if (!IsEndAllowed(shape, distance, Distance(start, turn)))
        {
            source.Report(SyntaxRules.InvalidSlideEnd, $"The slide \"{segment}\" in \"{noteText.ToString()}\" cannot be drawn: its end button is not allowed for '{shapeText}'.", noteText);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets whether a shape can end at a button.
    /// </summary>
    /// <param name="shape">The shape character.</param>
    /// <param name="distance">The clockwise distance from the start button to the end button, 0 to 7.</param>
    /// <param name="turnDistance">For <c>V</c>, the clockwise distance from the start button to the turn button.</param>
    /// <returns><see langword="true"/> when the official table allows the end button.</returns>
    internal static bool IsEndAllowed(char shape, int distance, int turnDistance)
    {
        return shape switch
        {
            '-' => distance >= 2 && distance <= 6,
            '^' => distance != 0 && distance != 4,
            'v' => distance != 0 && distance != 4,
            's' or 'z' or 'w' => distance == 4,
            'V' => (turnDistance == 6 && distance >= 1 && distance <= 4) || (turnDistance == 2 && distance >= 4 && distance <= 7),
            _ => true,
        };
    }

    /// <summary>
    /// Gets the clockwise distance between two buttons.
    /// </summary>
    /// <param name="from">The first button, 1 to 8.</param>
    /// <param name="to">The second button, 1 to 8.</param>
    /// <returns>The distance, 0 to 7.</returns>
    internal static int Distance(int from, int to)
    {
        return ((to - from) % 8 + 8) % 8;
    }

    /// <summary>
    /// Reads a button from 1 to 8.
    /// </summary>
    /// <param name="text">The slide text.</param>
    /// <param name="index">The index of the button; moves past it when it was read.</param>
    /// <param name="button">Receives the button.</param>
    /// <returns><see langword="true"/> when a button was read.</returns>
    private static bool TryReadButton(ReadOnlySpan<char> text, ref int index, out int button)
    {
        button = 0;
        if (index >= text.Length || text[index] < '1' || text[index] > '8')
        {
            return false;
        }
        button = text[index] - '0';
        index++;

        return true;
    }
}
