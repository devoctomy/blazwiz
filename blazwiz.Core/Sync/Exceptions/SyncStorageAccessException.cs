namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// Thrown when the storage in use cannot be reached, e.g. no folder has been picked or the browser needs permission to
/// it again.
/// </summary>
public class SyncStorageAccessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageAccessException"/> class.
    /// </summary>
    public SyncStorageAccessException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageAccessException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SyncStorageAccessException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageAccessException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SyncStorageAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
