using System;

namespace MajSimai.Logging;

/// <summary>
/// Receives the log messages written by MajSimai. Set an implementation with <see cref="SimaiLogger.Current"/>.
/// </summary>
/// <remarks>
/// Parsing runs on several threads, so implementations must be thread safe.
/// </remarks>
public interface ISimaiLogger
{
    /// <summary>
    /// Gets whether messages of <paramref name="level"/> are written. MajSimai skips building messages that would not be
    /// written.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when messages of <paramref name="level"/> are written.</returns>
    bool IsEnabled(SimaiLogLevel level);

    /// <summary>
    /// Writes a message.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message, including where in the input it applies.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    void Log(SimaiLogLevel level, string message, Exception? exception);
}
