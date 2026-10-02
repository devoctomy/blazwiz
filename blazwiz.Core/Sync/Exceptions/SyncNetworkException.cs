namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// The remote store could not be reached. The pass stops and the app is marked offline.
/// </summary>
public class SyncNetworkException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncNetworkException"/> class.
    /// </summary>
    public SyncNetworkException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncNetworkException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SyncNetworkException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncNetworkException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SyncNetworkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
