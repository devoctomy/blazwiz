namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// A conditional upload or delete was refused (412) because the remote object changed since it was listed.
/// </summary>
public class SyncPreconditionFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreconditionFailedException"/> class.
    /// </summary>
    public SyncPreconditionFailedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SyncPreconditionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SyncPreconditionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
