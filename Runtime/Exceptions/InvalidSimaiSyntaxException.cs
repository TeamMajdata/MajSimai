namespace MajSimai;

/// <summary>
/// Thrown when a simai declaration, such as a BPM or HS change, has a value that cannot be read.
/// </summary>
public class InvalidSimaiSyntaxException : InvalidSimaiMarkupException
{
    /// <summary>
    /// Creates the exception with the default message.
    /// </summary>
    /// <param name="line">The line of the problem, starting from 1.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text that could not be read.</param>
    public InvalidSimaiSyntaxException(int line, int column, string content)
        : base(line, column, content)
    {
    }

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="line">The line of the problem, starting from 1.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text that could not be read.</param>
    /// <param name="message">The description of the problem.</param>
    public InvalidSimaiSyntaxException(int line, int column, string content, string message)
        : base(line, column, content, message)
    {
    }
}
