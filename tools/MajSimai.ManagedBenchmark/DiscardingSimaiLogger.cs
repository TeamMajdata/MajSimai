using System;
using MajSimai.Logging;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// A logger that accepts the levels of the default logger but writes nothing. The parser decides from
/// <see cref="ISimaiLogger.IsEnabled"/> whether to run some lenient syntax checks and to build their messages, so
/// turning logging off would skip work the default logger causes. This logger keeps that work and drops only the
/// console output, which BenchmarkDotNet would otherwise carry from the benchmark process to its own log.
/// </summary>
internal sealed class DiscardingSimaiLogger : ISimaiLogger
{
    /// <summary>
    /// The levels the logger accepts, from the default <see cref="ConsoleSimaiLogger"/>.
    /// </summary>
    private readonly ConsoleSimaiLogger _levels = new();

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static DiscardingSimaiLogger Instance { get; } = new();

    /// <summary>
    /// Gets whether messages of a level are accepted, as the default logger does.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when the default logger would write messages of <paramref name="level"/>.</returns>
    public bool IsEnabled(SimaiLogLevel level)
    {
        return _levels.IsEnabled(level);
    }

    /// <summary>
    /// Drops a message.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public void Log(SimaiLogLevel level, string message, Exception? exception)
    {
    }
}
