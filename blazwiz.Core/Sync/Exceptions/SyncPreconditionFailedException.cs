using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// Thrown when a conditional write fails because the remote file changed since it was listed.
/// </summary>
public class SyncPreconditionFailedException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreconditionFailedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncPreconditionFailedException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreconditionFailedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncPreconditionFailedException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
