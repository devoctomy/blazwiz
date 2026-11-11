using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Sync.Exceptions;

/// <summary>
/// Thrown when a protected profile's passphrase change cannot go on: this device is offline, or the profile could not
/// be synced (<see cref="SyncErrorCodes.RotationOffline"/>, <see cref="SyncErrorCodes.RotationSyncFailed"/>).
/// </summary>
public class SyncKeyRotationException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncKeyRotationException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncKeyRotationException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncKeyRotationException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public SyncKeyRotationException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
