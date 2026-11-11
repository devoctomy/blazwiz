namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// Where a profile stands on this device.
/// </summary>
public enum E2eProfileState
{
    /// <summary>
    /// A profile set up in code with no passphrase yet.
    /// </summary>
    NotSetUp,

    /// <summary>
    /// Locked: its files cannot be read or written until it is unlocked.
    /// </summary>
    Locked,

    /// <summary>
    /// Unlocked in this session.
    /// </summary>
    Unlocked,

    /// <summary>
    /// Unlocked, but a passphrase change was cut short: it must be finished before the profile is used.
    /// </summary>
    RotationInProgress,
}
