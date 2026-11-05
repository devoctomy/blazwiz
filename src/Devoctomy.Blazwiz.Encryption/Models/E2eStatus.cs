namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// Where the profiles set up in code stand for the signed in user on this device.
/// </summary>
public enum E2eStatus
{
    /// <summary>
    /// Not checked yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// Their keys cannot be reached in this session.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Their passphrase has not been set yet.
    /// </summary>
    Disabled,

    /// <summary>
    /// Their passphrase is set, and they are locked on this device.
    /// </summary>
    NeedsSetup,

    /// <summary>
    /// Their keys are in memory.
    /// </summary>
    Unlocked,
}
