using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MajSimai.Parsing;

namespace MajSimai.Diagnostics;

/// <summary>
/// Checks simai files and charts for grammar problems and reports them as <see cref="SimaiDiagnostic"/> values,
/// instead of stopping at the first one like <see cref="SimaiParser"/> does.
/// </summary>
/// <remarks>
/// <para>
/// The checker reads the text with the same rules as the parser in strict mode, and in addition checks that buttons and
/// sensors exist, that every slide end button is allowed for its shape (following the official simai table), that slide
/// durations are consistent, and that Slide Code follows its syntax and only connects nodes and circles that can be
/// connected.
/// </para>
/// <para>
/// <see cref="SimaiDiagnosticSeverity.Error"/> means the text breaks the grammar: exactly the texts with no errors parse
/// in <see cref="SimaiSyntaxTolerance.Strict"/> mode. <see cref="SimaiDiagnosticSeverity.Warning"/> means the text is
/// valid but questionable, such as Slide Code that is hard to read or a field that is set twice.
/// </para>
/// </remarks>
public static class SimaiSyntaxChecker
{
    /// <summary>
    /// Checks a whole simai file: its fields and its seven charts.
    /// </summary>
    /// <param name="content">The simai text.</param>
    /// <returns>The problems, ordered by line; empty when there are none.</returns>
    public static IReadOnlyList<SimaiDiagnostic> Check(ReadOnlySpan<char> content)
    {
        var diagnostics = new List<SimaiDiagnostic>();
        var chartLines = new int[SimaiMetadata.ChartCount];
        var metadata = MetadataReader.Read(content, string.Empty, new SyntaxReporter(diagnostics, null, 0), chartLines);
        var fumens = metadata.Fumens;
        for (var i = 0; i < fumens.Length; i++)
        {
            if (fumens[i].Length == 0)
            {
                continue;
            }
            var reporter = new SyntaxReporter(diagnostics, i + 1, chartLines[i] - 1);
            ChartParser.Parse(string.Empty, string.Empty, fumens[i].AsSpan(), fumens[i], reporter, false);
        }

        return Sort(diagnostics);
    }

    /// <summary>
    /// Reads a stream to its end with <paramref name="encoding"/> (or the encoding of its byte order mark) and checks it
    /// as a whole simai file.
    /// </summary>
    /// <param name="contentStream">The stream holding the file.</param>
    /// <param name="encoding">The encoding of the text; <see langword="null"/> uses UTF-8.</param>
    /// <returns>The problems, ordered by line; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentStream"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<SimaiDiagnostic> Check(Stream contentStream, Encoding? encoding = null)
    {
        using var content = ContentDecoder.Decode(contentStream, encoding ?? Encoding.UTF8);

        return Check(content.Span);
    }

    /// <summary>
    /// Checks one chart text, such as the value of <c>&amp;inote_5</c>.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The problems, ordered by line, with lines counted in the chart text; empty when there are none.</returns>
    public static IReadOnlyList<SimaiDiagnostic> CheckChart(ReadOnlySpan<char> fumen)
    {
        var diagnostics = new List<SimaiDiagnostic>();
        ChartParser.Parse(string.Empty, string.Empty, fumen, string.Empty, new SyntaxReporter(diagnostics, null, 0), false);

        return Sort(diagnostics);
    }

    /// <summary>
    /// Orders diagnostics by line and column, keeping the order they were found in otherwise.
    /// </summary>
    /// <param name="diagnostics">The diagnostics.</param>
    /// <returns>The ordered diagnostics.</returns>
    private static IReadOnlyList<SimaiDiagnostic> Sort(List<SimaiDiagnostic> diagnostics)
    {
        var indexed = new (SimaiDiagnostic Diagnostic, int Index)[diagnostics.Count];
        for (var i = 0; i < indexed.Length; i++)
        {
            indexed[i] = (diagnostics[i], i);
        }
        Array.Sort(indexed, (a, b) =>
        {
            var byLine = a.Diagnostic.Line.CompareTo(b.Diagnostic.Line);
            if (byLine != 0)
            {
                return byLine;
            }
            var byColumn = a.Diagnostic.Column.CompareTo(b.Diagnostic.Column);

            return byColumn != 0 ? byColumn : a.Index.CompareTo(b.Index);
        });
        var result = new SimaiDiagnostic[indexed.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = indexed[i].Diagnostic;
        }

        return result;
    }
}
