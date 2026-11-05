using System;
using System.Collections.Concurrent;
using MajSimai.Logging;

namespace MajSimai.Tests.Logging;

/// <summary>
/// A logger that keeps every message it receives.
/// </summary>
internal sealed class CapturingLogger : ISimaiLogger
{
    /// <summary>
    /// Creates a logger that captures messages of <paramref name="minimumLevel"/> and above.
    /// </summary>
    /// <param name="minimumLevel">The lowest captured level.</param>
    public CapturingLogger(SimaiLogLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
    }

    /// <summary>
    /// Gets the lowest captured level.
    /// </summary>
    public SimaiLogLevel MinimumLevel { get; }

    /// <summary>
    /// Gets the captured messages.
    /// </summary>
    public ConcurrentQueue<(SimaiLogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    /// <inheritdoc/>
    public bool IsEnabled(SimaiLogLevel level)
    {
        return level >= MinimumLevel && level != SimaiLogLevel.None;
    }

    /// <inheritdoc/>
    public void Log(SimaiLogLevel level, string message, Exception? exception)
    {
        Entries.Enqueue((level, message, exception));
    }
}
