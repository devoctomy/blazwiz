namespace blazwiz.Portal.Application.Exceptions;

/// <summary>
/// A notes operation that cannot be completed, e.g. an invalid or duplicate name. The message is suitable to show to the user.
/// </summary>
public class NotesException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotesException"/> class.
    /// </summary>
    /// <param name="message">The message to show to the user.</param>
    public NotesException(string message)
        : base(message)
    {
    }
}
