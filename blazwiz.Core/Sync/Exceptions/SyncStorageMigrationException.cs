using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// Thrown when files could not be moved to another storage, or cannot be changed while they are being moved.
/// </summary>
public class SyncStorageMigrationException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageMigrationException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncStorageMigrationException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageMigrationException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncStorageMigrationException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
