using System;

namespace MajSimai.Logging;

/// <summary>
/// Writes MajSimai log messages to the console. This is the default logger, with a minimum level of
/// <see cref="SimaiLogLevel.Warning"/>.
/// </summary>
public sealed class ConsoleSimaiLogger : ISimaiLogger
{
    /// <summary>
    /// Keeps messages written from several threads from mixing.
    /// </summary>
    private static readonly object s_writeLock = new();

    /// <summary>
    /// Creates a console logger.
    /// </summary>
    /// <param name="minimumLevel">The lowest level that is written.</param>
    public ConsoleSimaiLogger(SimaiLogLevel minimumLevel = SimaiLogLevel.Warning)
    {
        MinimumLevel = minimumLevel;
    }

    /// <summary>
    /// Gets the lowest level that is written.
    /// </summary>
    public SimaiLogLevel MinimumLevel { get; }

    /// <summary>
    /// Gets whether messages of <paramref name="level"/> are written.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when <paramref name="level"/> is at least <see cref="MinimumLevel"/>.</returns>
    public bool IsEnabled(SimaiLogLevel level)
    {
        return level != SimaiLogLevel.None && level >= MinimumLevel;
    }

    /// <summary>
    /// Writes a message to the standard output, followed by the exception if there is one.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public void Log(SimaiLogLevel level, string message, Exception? exception)
    {
        if (!IsEnabled(level))
        {
            return;
        }
        var text = exception is null
            ? $"[MajSimai] [{level}] {message}"
            : $"[MajSimai] [{level}] {message}{Environment.NewLine}{exception}";
        lock (s_writeLock)
        {
            Console.WriteLine(text);
        }
    }
}
