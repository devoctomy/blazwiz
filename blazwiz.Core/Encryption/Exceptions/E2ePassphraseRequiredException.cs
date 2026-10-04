namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when this device's keys cannot be unlocked with the passkey (e.g. it was registered again since), so the
/// passphrase is needed instead.
/// </summary>
public class E2ePassphraseRequiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class.
    /// </summary>
    public E2ePassphraseRequiredException()
        : base("Your passkey cannot unlock this device's keys. Enter your passphrase instead.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public E2ePassphraseRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public E2ePassphraseRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
