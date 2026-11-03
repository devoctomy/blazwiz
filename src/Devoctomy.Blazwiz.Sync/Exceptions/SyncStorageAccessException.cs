using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Sync.Exceptions;

/// <summary>
/// Thrown when the storage the files are kept in on this device cannot be used, e.g. the folder is not allowed.
/// </summary>
public class SyncStorageAccessException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageAccessException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncStorageAccessException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStorageAccessException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncStorageAccessException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
