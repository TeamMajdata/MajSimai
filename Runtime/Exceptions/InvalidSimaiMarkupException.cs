using System;

namespace MajSimai;

/// <summary>
/// Thrown when simai text cannot be read, with the position of the problem.
/// </summary>
public class InvalidSimaiMarkupException : FormatException
{
    /// <summary>
    /// Creates the exception with the default message.
    /// </summary>
    /// <param name="line">The line of the problem, starting from 1.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text that could not be read.</param>
    public InvalidSimaiMarkupException(int line, int column, string content)
        : base()
    {
        Line = line;
        Column = column;
        Content = content;
    }

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="line">The line of the problem, starting from 1.</param>
    /// <param name="column">The column of the problem.</param>
    /// <param name="content">The text that could not be read.</param>
    /// <param name="message">The description of the problem.</param>
    public InvalidSimaiMarkupException(int line, int column, string content, string message)
        : base(message)
    {
        Line = line;
        Column = column;
        Content = content;
    }

    /// <summary>
    /// Gets the line of the problem, starting from 1.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column of the problem.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the text that could not be read.
    /// </summary>
    public string Content { get; }
}
