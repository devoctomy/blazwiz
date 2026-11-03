using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Sync.Exceptions;

/// <summary>
/// Thrown when the remote store cannot be reached.
/// </summary>
public class SyncNetworkException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncNetworkException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncNetworkException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncNetworkException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncNetworkException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
