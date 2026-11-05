using System;
using System.Collections.Generic;
using MajSimai.Diagnostics;
using MajSimai.Logging;

namespace MajSimai.Parsing;

/// <summary>
/// Decides what happens when a <see cref="SyntaxRule"/> is broken: throw, log, or collect a
/// <see cref="SimaiDiagnostic"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Lenient: <see cref="SyntaxRuleKind.Invalid"/> rules throw; <see cref="SyntaxRuleKind.Undefined"/> rules are
/// logged and parsing continues the MajSimai way; strict checks do not run.</item>
/// <item>Strict: <see cref="SyntaxRuleKind.Invalid"/> and <see cref="SyntaxRuleKind.Undefined"/> rules throw.</item>
/// <item>Collecting: nothing throws; every broken rule, <see cref="SyntaxRuleKind.Advice"/> included, becomes a
/// diagnostic, and the caller recovers from <see cref="SyntaxRuleKind.Invalid"/> rules by skipping the text.</item>
/// </list>
/// The lenient and strict reporters keep no state and can be shared between threads; a collecting reporter cannot.
/// </remarks>
internal sealed class SyntaxReporter
{
    /// <summary>
    /// The diagnostics collected, or <see langword="null"/> when the reporter does not collect.
    /// </summary>
    private readonly List<SimaiDiagnostic>? _diagnostics;

    /// <summary>
    /// The chart (1 to 7) diagnostics belong to, or <see langword="null"/>.
    /// </summary>
    private readonly int? _chartIndex;

    /// <summary>
    /// Added to the line of every diagnostic, to turn chart lines into file lines.
    /// </summary>
    private readonly int _lineOffset;

    /// <summary>
    /// Creates a reporter that throws or logs.
    /// </summary>
    /// <param name="tolerance">How strictly the grammar is followed.</param>
    private SyntaxReporter(SimaiSyntaxTolerance tolerance)
    {
        Tolerance = tolerance;
    }

    /// <summary>
    /// Creates a reporter that collects diagnostics instead of throwing.
    /// </summary>
    /// <param name="diagnostics">Receives the diagnostics.</param>
    /// <param name="chartIndex">The chart (1 to 7) diagnostics belong to, or <see langword="null"/>.</param>
    /// <param name="lineOffset">Added to the line of every diagnostic.</param>
    /// <param name="tolerance">The rules the text is read with: <see cref="SimaiSyntaxTolerance.Strict"/> (the default)
    /// also runs the strict checks, <see cref="SimaiSyntaxTolerance.Lenient"/> reads the text as the lenient parser does
    /// and only collects what it skips or tolerates.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    public SyntaxReporter(List<SimaiDiagnostic> diagnostics,
                          int? chartIndex,
                          int lineOffset,
                          SimaiSyntaxTolerance tolerance = SimaiSyntaxTolerance.Strict)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _chartIndex = chartIndex;
        _lineOffset = lineOffset;
        Tolerance = tolerance;
    }

    /// <summary>
    /// Gets the shared lenient reporter.
    /// </summary>
    public static SyntaxReporter Lenient { get; } = new(SimaiSyntaxTolerance.Lenient);

    /// <summary>
    /// Gets the shared strict reporter.
    /// </summary>
    public static SyntaxReporter Strict { get; } = new(SimaiSyntaxTolerance.Strict);

    /// <summary>
    /// Gets how strictly the grammar is followed. A collecting reporter runs every strict check unless it was created
    /// with <see cref="SimaiSyntaxTolerance.Lenient"/>.
    /// </summary>
    public SimaiSyntaxTolerance Tolerance { get; }

    /// <summary>
    /// Gets whether the strict checks (note shapes, slide paths, Slide Code) run.
    /// </summary>
    public bool ChecksStrictly => Tolerance == SimaiSyntaxTolerance.Strict;

    /// <summary>
    /// Gets whether the reporter collects diagnostics instead of throwing.
    /// </summary>
    public bool IsCollecting => _diagnostics is not null;

    /// <summary>
    /// Gets the shared reporter for parser options.
    /// </summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The lenient or strict reporter.</returns>
    public static SyntaxReporter For(SimaiParserOptions? options)
    {
        return options?.IsStrict == true ? Strict : Lenient;
    }

    /// <summary>
    /// Gets whether reporting a broken rule has any effect, so callers can skip building the message when it has none:
    /// it has none for advice when not collecting, and for undefined text in lenient mode when the log level is off.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when <see cref="Report"/> would collect, throw or log.</returns>
    public bool IsReported(SyntaxRule rule)
    {
        if (_diagnostics is not null)
        {
            return true;
        }

        return rule.Kind switch
        {
            SyntaxRuleKind.Invalid => true,
            SyntaxRuleKind.Undefined => ChecksStrictly || SimaiLogger.IsEnabled(rule.LenientLogLevel),
            _ => false,
        };
    }

    /// <summary>
    /// Reports a broken rule. Returns only when parsing can continue: after an <see cref="SyntaxRuleKind.Invalid"/> rule
    /// that happens only when collecting, and the caller must skip the text.
    /// </summary>
    /// <param name="rule">The broken rule.</param>
    /// <param name="line">The line of the problem.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text the problem is about.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when the rule stops parsing in this mode.</exception>
    public void Report(SyntaxRule rule, int line, int column, string content, string message)
    {
        if (_diagnostics is not null)
        {
            _diagnostics.Add(new SimaiDiagnostic(rule.Severity, rule.Code, message, line + _lineOffset, column, content, _chartIndex));
            return;
        }
        switch (rule.Kind)
        {
            case SyntaxRuleKind.Invalid:
                throw CreateException(rule, line, column, content, message);
            case SyntaxRuleKind.Undefined when ChecksStrictly:
                throw CreateException(rule, line, column, content, message);
            case SyntaxRuleKind.Undefined:
                if (SimaiLogger.IsEnabled(rule.LenientLogLevel))
                {
                    Log(rule.LenientLogLevel, $"{message} (line {line}, column {column}, {rule.Code})");
                }
                return;
            default:
                return;
        }
    }

    /// <summary>
    /// Creates the exception of a rule.
    /// </summary>
    /// <param name="rule">The broken rule.</param>
    /// <param name="line">The line of the problem.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text the problem is about.</param>
    /// <param name="message">The description of the problem.</param>
    /// <returns>The exception.</returns>
    private static InvalidSimaiMarkupException CreateException(SyntaxRule rule, int line, int column, string content, string message)
    {
        return rule.ExceptionKind switch
        {
            SyntaxExceptionKind.Syntax => new InvalidSimaiSyntaxException(line, column, content, message),
            SyntaxExceptionKind.Note => new InvalidSimaiNoteException(line, column, content, message),
            _ => new InvalidSimaiMarkupException(line, column, content, message),
        };
    }

    /// <summary>
    /// Writes a message at a level.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="message">The message.</param>
    private static void Log(SimaiLogLevel level, string message)
    {
        switch (level)
        {
            case SimaiLogLevel.Error:
                SimaiLogger.Error(message);
                break;
            case SimaiLogLevel.Warning:
                SimaiLogger.Warning(message);
                break;
            case SimaiLogLevel.Info:
                SimaiLogger.Info(message);
                break;
            default:
                SimaiLogger.Debug(message);
                break;
        }
    }
}
