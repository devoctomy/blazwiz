namespace blazwiz.Portal.Models.Sync;

/// <summary>
/// The end to end encryption dialog shown in settings. Passphrases are only ever entered in these dialogs.
/// </summary>
public enum E2eDialogKind
{
    /// <summary>
    /// Turn encryption on with a new passphrase.
    /// </summary>
    Enable,

    /// <summary>
    /// Set this device up with the existing passphrase.
    /// </summary>
    SetUp,

    /// <summary>
    /// Change the passphrase.
    /// </summary>
    ChangePassphrase,

    /// <summary>
    /// Turn encryption off.
    /// </summary>
    Disable,
}
