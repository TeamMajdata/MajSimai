using System;
using System.Threading;

namespace MajSimai.Logging;

/// <summary>
/// The logger used by MajSimai, and helpers to write to it.
/// </summary>
public static class SimaiLogger
{
    /// <summary>
    /// The logger that receives messages.
    /// </summary>
    private static ISimaiLogger s_current = new ConsoleSimaiLogger();

    /// <summary>
    /// Gets or sets the logger that receives MajSimai messages. Defaults to a <see cref="ConsoleSimaiLogger"/> with a
    /// minimum level of <see cref="SimaiLogLevel.Warning"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    public static ISimaiLogger Current
    {
        get => Volatile.Read(ref s_current);
        set => Volatile.Write(ref s_current, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// Gets whether messages of <paramref name="level"/> are written by <see cref="Current"/>.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when messages of <paramref name="level"/> are written.</returns>
    public static bool IsEnabled(SimaiLogLevel level)
    {
        return Current.IsEnabled(level);
    }

    /// <summary>
    /// Writes a <see cref="SimaiLogLevel.Debug"/> message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public static void Debug(string message, Exception? exception = null)
    {
        Write(SimaiLogLevel.Debug, message, exception);
    }

    /// <summary>
    /// Writes an <see cref="SimaiLogLevel.Info"/> message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public static void Info(string message, Exception? exception = null)
    {
        Write(SimaiLogLevel.Info, message, exception);
    }

    /// <summary>
    /// Writes a <see cref="SimaiLogLevel.Warning"/> message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public static void Warning(string message, Exception? exception = null)
    {
        Write(SimaiLogLevel.Warning, message, exception);
    }

    /// <summary>
    /// Writes an <see cref="SimaiLogLevel.Error"/> message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    public static void Error(string message, Exception? exception = null)
    {
        Write(SimaiLogLevel.Error, message, exception);
    }

    /// <summary>
    /// Writes a message when <see cref="Current"/> accepts its level.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message.</param>
    /// <param name="exception">The exception that caused the message, if any.</param>
    private static void Write(SimaiLogLevel level, string message, Exception? exception)
    {
        var logger = Current;
        if (logger.IsEnabled(level))
        {
            logger.Log(level, message, exception);
        }
    }
}
