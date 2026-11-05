using System;

namespace MajSimai.Diagnostics;

/// <summary>
/// A problem found by <see cref="SimaiSyntaxChecker"/>.
/// </summary>
public sealed class SimaiDiagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="severity">How serious the problem is.</param>
    /// <param name="code">The rule code, one of <see cref="SimaiDiagnosticCodes"/>.</param>
    /// <param name="message">The description of the problem.</param>
    /// <param name="line">The line of the problem, starting from 1.</param>
    /// <param name="column">The column of the problem, starting from 1.</param>
    /// <param name="content">The text the problem is about.</param>
    /// <param name="chartIndex">The chart (1 to 7) the problem is in, or <see langword="null"/> for the fields of the file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/>, <paramref name="message"/> or <paramref name="content"/> is <see langword="null"/>.</exception>
    public SimaiDiagnostic(SimaiDiagnosticSeverity severity,
                           string code,
                           string message,
                           int line,
                           int column,
                           string content,
                           int? chartIndex)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Line = line;
        Column = column;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ChartIndex = chartIndex;
    }

    /// <summary>
    /// Gets how serious the problem is.
    /// </summary>
    public SimaiDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the rule code, one of <see cref="SimaiDiagnosticCodes"/>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the description of the problem.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the line of the problem, starting from 1. For a whole file it is the line in the file; for a single chart it is
    /// the line in the chart text.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column of the problem, starting from 1. In a chart it is counted on the line after its leading whitespace
    /// is removed, as the chart text is stored in <see cref="SimaiChart.Fumen"/>.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the text the problem is about.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the chart (1 to 7, as in <c>&amp;inote_N</c>) the problem is in, or <see langword="null"/> for the fields of
    /// the file.
    /// </summary>
    public int? ChartIndex { get; }

    /// <summary>
    /// Formats the diagnostic as <c>line:column: severity code: message</c>.
    /// </summary>
    /// <returns>The formatted diagnostic.</returns>
    public override string ToString()
    {
        var chart = ChartIndex is null ? string.Empty : $"inote_{ChartIndex} ";

        return $"{chart}{Line}:{Column}: {Severity} {Code}: {Message}";
    }
}
