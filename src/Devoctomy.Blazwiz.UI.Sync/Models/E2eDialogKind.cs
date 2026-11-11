namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// The dialogs of the end to end encryption settings.
/// </summary>
public enum E2eDialogKind
{
    /// <summary>
    /// Set the first passphrase of a profile set up in code.
    /// </summary>
    SetPassphrase,

    /// <summary>
    /// Create a protected profile with its own passphrase.
    /// </summary>
    Create,

    /// <summary>
    /// Unlock a profile with its passphrase.
    /// </summary>
    Unlock,

    /// <summary>
    /// Change a profile's passphrase.
    /// </summary>
    ChangePassphrase,

    /// <summary>
    /// Delete a protected profile the user created, and its files.
    /// </summary>
    Delete,

    /// <summary>
    /// Finish a passphrase change that was cut short.
    /// </summary>
    FinishRotation,
}
