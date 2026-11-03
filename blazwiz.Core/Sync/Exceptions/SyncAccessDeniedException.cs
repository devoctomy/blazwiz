using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Sync.Exceptions;

/// <summary>
/// Thrown when the remote store refuses the credentials, or there are none.
/// </summary>
public class SyncAccessDeniedException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAccessDeniedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncAccessDeniedException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAccessDeniedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncAccessDeniedException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
