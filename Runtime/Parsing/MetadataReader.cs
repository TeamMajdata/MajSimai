using System;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Reads the <c>&amp;key=value</c> fields of a simai file. Chart texts are kept as text; their notes are not read.
/// </summary>
/// <remarks>
/// Lines are trimmed and only lines starting with <c>&amp;</c> are fields; the name and the value of a field are trimmed
/// too. Song and chart fields keep their first non-empty value, while <c>&amp;first</c> and <c>&amp;inote_N</c> keep
/// their last one. A chart text runs until the next line that starts with <c>&amp;</c>; <c>||</c> comments only have a
/// meaning inside chart texts. Other fields become <see cref="SimaiCommand"/> values. A line starting with <c>&amp;</c>
/// without <c>=</c> is ignored in lenient mode and throws in strict mode.
/// </remarks>
internal static class MetadataReader
{
    /// <summary>
    /// Reads the fields of a simai file.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="hash">The hash of the file content.</param>
    /// <param name="reporter">Decides what happens when a field breaks the grammar.</param>
    /// <param name="chartLines">When not <see langword="null"/>, receives for each chart the line of the file where its
    /// trimmed text starts.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidSimaiMarkupException">Thrown in strict mode when a line starting with <c>&amp;</c> has no <c>=</c>.</exception>
    /// <exception cref="InvalidSimaiSyntaxException">Thrown in strict mode when <c>&amp;first</c> is not a number.</exception>
    public static SimaiMetadata Read(ReadOnlySpan<char> content, string hash, SyntaxReporter reporter, int[]? chartLines = null)
    {
        if (hash is null)
        {
            throw new ArgumentNullException(nameof(hash));
        }
        var title = string.Empty;
        var artist = string.Empty;
        var finalDesigner = string.Empty;
        var offset = 0f;
        var designers = CreateEmptyValues();
        var levels = CreateEmptyValues();
        var fumens = CreateEmptyValues();
        using var commands = new PooledBuffer<SimaiCommand>();
        using var lines = SplitLines(content);
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = content[lines[lineIndex]].Trim();
            if (line.IsEmpty || line[0] != '&')
            {
                continue;
            }
            var lineNumber = lineIndex + 1;
            var separator = line.IndexOf('=');
            if (separator == -1)
            {
                var text = line.ToString();
                reporter.Report(SyntaxRules.FieldWithoutValue, lineNumber, 1, text, $"The line \"{text}\" starts with '&' but has no '='; it is ignored.");
                continue;
            }
            var name = line.Slice(1, separator - 1).Trim();
            var value = line.Slice(separator + 1).Trim();
            if (name.IsEmpty)
            {
                continue;
            }
            if (TryGetIndexedField(name, "inote_", out var chartIndex))
            {
                ReportDuplicate(reporter, fumens[chartIndex].Length != 0, lineNumber, name);
                var startLine = lineNumber;
                fumens[chartIndex] = ReadChartText(content, lines, value, ref lineIndex, ref startLine);
                if (chartLines is not null)
                {
                    chartLines[chartIndex] = startLine;
                }
            }
            else if (TryGetIndexedField(name, "des_", out var designerIndex))
            {
                SetFirstValue(reporter, value, ref designers[designerIndex], lineNumber, name);
            }
            else if (TryGetIndexedField(name, "lv_", out var levelIndex))
            {
                SetFirstValue(reporter, value, ref levels[levelIndex], lineNumber, name);
            }
            else if (name.SequenceEqual("title".AsSpan()))
            {
                SetFirstValue(reporter, value, ref title, lineNumber, name);
            }
            else if (name.SequenceEqual("artist".AsSpan()))
            {
                SetFirstValue(reporter, value, ref artist, lineNumber, name);
            }
            else if (name.SequenceEqual("des".AsSpan()))
            {
                SetFirstValue(reporter, value, ref finalDesigner, lineNumber, name);
            }
            else if (name.SequenceEqual("first".AsSpan()))
            {
                if (!DurationReader.TryParseFloat(value, out offset))
                {
                    offset = 0;
                    if (!value.IsEmpty)
                    {
                        var text = value.ToString();
                        reporter.Report(SyntaxRules.InvalidOffset, lineNumber, 1, text, $"The &first value \"{text}\" is not a number; 0 is used.");
                    }
                }
            }
            else
            {
                commands.Add(new SimaiCommand(name.ToString(), value.ToString()));
            }
        }
        if (string.IsNullOrEmpty(finalDesigner))
        {
            // The last chart designer that is set.
            foreach (var designer in designers)
            {
                if (!string.IsNullOrEmpty(designer))
                {
                    finalDesigner = designer;
                }
            }
        }

        return SimaiMetadata.Create(title, artist, offset, designers, levels, fumens, commands.ToArray(), hash, finalDesigner);
    }

    /// <summary>
    /// Reads a chart text: the value of the <c>&amp;inote_N</c> line followed by every line up to the next field. Each
    /// line is trimmed, and the whole text is trimmed at the end.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <param name="lines">The line ranges of the text.</param>
    /// <param name="firstLine">The value on the <c>&amp;inote_N</c> line.</param>
    /// <param name="lineIndex">The index of the <c>&amp;inote_N</c> line; receives the index of the last chart line.</param>
    /// <param name="startLine">The number of the <c>&amp;inote_N</c> line; receives the line where the trimmed text starts.</param>
    /// <returns>The chart text.</returns>
    private static string ReadChartText(ReadOnlySpan<char> content,
                                        PooledBuffer<Range> lines,
                                        ReadOnlySpan<char> firstLine,
                                        ref int lineIndex,
                                        ref int startLine)
    {
        using var text = new PooledBuffer<char>(256);
        text.AddRange(firstLine);
        text.Add('\n');
        for (lineIndex++; lineIndex < lines.Count; lineIndex++)
        {
            var line = content[lines[lineIndex]].Trim();
            if (!line.IsEmpty && line[0] == '&')
            {
                lineIndex--;
                break;
            }
            text.AddRange(line);
            text.Add('\n');
        }
        ReadOnlySpan<char> chart = text.AsSpan();
        var trimmed = chart.TrimStart();
        startLine += LegacySplit.Count(chart.Slice(0, chart.Length - trimmed.Length), '\n');

        return trimmed.TrimEnd().ToString();
    }

    /// <summary>
    /// Matches a field name such as <c>lv_3</c>.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="prefix">The name before the number, such as <c>lv_</c>.</param>
    /// <param name="index">Receives the zero based chart index.</param>
    /// <returns><see langword="true"/> when the name is the prefix followed by a digit from 1 to 7.</returns>
    private static bool TryGetIndexedField(ReadOnlySpan<char> name, string prefix, out int index)
    {
        index = -1;
        if (name.Length != prefix.Length + 1 || !name.StartsWith(prefix.AsSpan()))
        {
            return false;
        }
        var digit = name[prefix.Length];
        if (digit < '1' || digit > '7')
        {
            return false;
        }
        index = digit - '1';

        return true;
    }

    /// <summary>
    /// Sets a field only when it has no value yet and the new value is not empty.
    /// </summary>
    /// <param name="reporter">Receives the advice when the field already has a value.</param>
    /// <param name="value">The new value.</param>
    /// <param name="field">The field.</param>
    /// <param name="lineNumber">The line of the field.</param>
    /// <param name="name">The field name.</param>
    private static void SetFirstValue(SyntaxReporter reporter, ReadOnlySpan<char> value, ref string field, int lineNumber, ReadOnlySpan<char> name)
    {
        if (value.IsEmpty)
        {
            return;
        }
        if (!string.IsNullOrEmpty(field))
        {
            ReportDuplicate(reporter, true, lineNumber, name);
            return;
        }
        field = value.ToString();
    }

    /// <summary>
    /// Reports a field that is set again, for the syntax checker.
    /// </summary>
    /// <param name="reporter">The reporter.</param>
    /// <param name="isDuplicate">Whether the field already has a value.</param>
    /// <param name="lineNumber">The line of the field.</param>
    /// <param name="name">The field name.</param>
    private static void ReportDuplicate(SyntaxReporter reporter, bool isDuplicate, int lineNumber, ReadOnlySpan<char> name)
    {
        if (!isDuplicate || !reporter.IsCollecting)
        {
            return;
        }
        var text = name.ToString();
        var kept = text.StartsWith("inote_", StringComparison.Ordinal) ? "the last one" : "the first one";
        reporter.Report(SyntaxRules.DuplicateField, lineNumber, 1, text, $"The field &{text} is set more than once; only {kept} is used.");
    }

    /// <summary>
    /// Creates the values of the seven charts, each an empty string.
    /// </summary>
    /// <returns>The array.</returns>
    private static string[] CreateEmptyValues()
    {
        var values = new string[SimaiMetadata.ChartCount];
        values.AsSpan().Fill(string.Empty);

        return values;
    }

    /// <summary>
    /// Finds the ranges of the lines of a text, split at <c>\n</c>.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <returns>A pooled buffer of line ranges that the caller must dispose.</returns>
    private static PooledBuffer<Range> SplitLines(ReadOnlySpan<char> content)
    {
        var lines = new PooledBuffer<Range>(256);
        var start = 0;
        while (true)
        {
            var end = content.Slice(start).IndexOf('\n');
            if (end == -1)
            {
                lines.Add(new Range(start, content.Length));
                return lines;
            }
            lines.Add(new Range(start, start + end));
            start += end + 1;
        }
    }
}
