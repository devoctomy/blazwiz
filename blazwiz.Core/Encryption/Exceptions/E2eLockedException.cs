namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when a file is encrypted with a key this device does not hold, or a file is written while encryption is on
/// but this device cannot encrypt yet.
/// </summary>
public class E2eLockedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class.
    /// </summary>
    public E2eLockedException()
        : base("End to end encryption is locked on this device.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public E2eLockedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public E2eLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
