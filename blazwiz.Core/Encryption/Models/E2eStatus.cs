namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// Where end to end encryption stands for the signed in user on this device.
/// </summary>
public enum E2eStatus
{
    /// <summary>
    /// Not checked yet, or the keys could not be reached and this device has no copy.
    /// </summary>
    Unknown,

    /// <summary>
    /// The keys store cannot be used in this session (e.g. the local placeholder sign in), so encryption is not offered.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Encryption is not enabled, and this session was not signed in with a passkey (PRF), so it cannot be enabled.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Encryption is enabled, but this session has no passkey (PRF) keys, so files cannot be read or synced until the
    /// passkey is used.
    /// </summary>
    Blocked,

    /// <summary>
    /// Encryption is not enabled, and can be.
    /// </summary>
    Disabled,

    /// <summary>
    /// Encryption is enabled, but this device has not been set up with the passphrase yet.
    /// </summary>
    NeedsSetup,

    /// <summary>
    /// Encryption is enabled and the keys are in memory.
    /// </summary>
    Unlocked,

    /// <summary>
    /// Encryption was turned off on another device, but this device still has its keys and may have encrypted
    /// changes not yet synced.
    /// </summary>
    DisabledElsewhere,
}
