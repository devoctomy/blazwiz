namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// The remote store refused the credentials (403). The credentials are refreshed once, then the pass fails.
/// </summary>
public class SyncAccessDeniedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAccessDeniedException"/> class.
    /// </summary>
    public SyncAccessDeniedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SyncAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SyncAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
