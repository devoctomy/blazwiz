namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when enabling encryption finds it was already enabled, e.g. on another device, so this device must be set
/// up with the existing passphrase instead.
/// </summary>
public class E2eAlreadyEnabledException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class.
    /// </summary>
    public E2eAlreadyEnabledException()
        : base("End to end encryption is already enabled on another device. Set up this device with your passphrase instead.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public E2eAlreadyEnabledException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public E2eAlreadyEnabledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
