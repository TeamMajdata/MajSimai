namespace MajSimai;

/// <summary>
/// Thrown in <see cref="SimaiSyntaxTolerance.Strict"/> mode when the text of a note breaks the note grammar, for example
/// a note with no button, a touch area letter that is not at the start of the note, an empty <c>*</c> part, a duration
/// with too many separators, or a slide whose end button is not allowed for its shape. In lenient mode such notes are
/// skipped and logged.
/// </summary>
/// <remarks>
/// <see cref="InvalidSimaiMarkupException.Line"/> and <see cref="InvalidSimaiMarkupException.Column"/> point at the start
/// of the timing point that holds the note, and <see cref="InvalidSimaiMarkupException.Content"/> is the note text.
/// </remarks>
public class InvalidSimaiNoteException : InvalidSimaiMarkupException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="line">The line of the timing point, starting from 1.</param>
    /// <param name="column">The column of the timing point, starting from 1.</param>
    /// <param name="content">The note text.</param>
    /// <param name="message">The description of the problem.</param>
    public InvalidSimaiNoteException(int line, int column, string content, string message)
        : base(line, column, content, message)
    {
    }
}
