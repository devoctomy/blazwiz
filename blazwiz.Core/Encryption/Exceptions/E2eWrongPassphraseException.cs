namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when the passphrase does not unwrap the master key.
/// </summary>
public class E2eWrongPassphraseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class.
    /// </summary>
    public E2eWrongPassphraseException()
        : base("The passphrase is not correct.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public E2eWrongPassphraseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public E2eWrongPassphraseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
