namespace MajSimai.Logging;

/// <summary>
/// The severity of a log message written by MajSimai.
/// </summary>
public enum SimaiLogLevel
{
    /// <summary>
    /// Detailed messages for diagnosing how a chart was read, such as note texts that could not be parsed.
    /// </summary>
    Debug,
    /// <summary>
    /// Messages about normal progress, such as a finished parse.
    /// </summary>
    Info,
    /// <summary>
    /// Messages about unusual input that was handled.
    /// </summary>
    Warning,
    /// <summary>
    /// Messages about input that could not be handled, such as a chart that failed to parse.
    /// </summary>
    Error,
    /// <summary>
    /// Disables logging when used as a minimum level.
    /// </summary>
    None
}
