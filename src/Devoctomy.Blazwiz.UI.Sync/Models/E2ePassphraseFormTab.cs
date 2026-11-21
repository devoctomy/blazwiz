namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// The tabs of the form a new passphrase is entered in.
/// </summary>
public enum E2ePassphraseFormTab
{
    /// <summary>
    /// The passphrase, entered twice, and the acknowledgement that it cannot be recovered.
    /// </summary>
    Passphrase,

    /// <summary>
    /// The Argon2id settings the passphrase key is derived with, and their benchmark.
    /// </summary>
    Advanced,
}
