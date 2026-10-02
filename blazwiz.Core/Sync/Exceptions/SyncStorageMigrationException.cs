namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// Thrown when files could not be moved to another storage, or cannot be changed while they are being moved.
/// </summary>
public class SyncStorageMigrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageMigrationException"/> class.
    /// </summary>
    public SyncStorageMigrationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SyncStorageMigrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SyncStorageMigrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
