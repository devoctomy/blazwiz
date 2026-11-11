namespace Devoctomy.Blazwiz.Encryption.Exceptions;

/// <summary>
/// The codes of end to end encryption errors users can see. The UI shows each with the text resource
/// <c>Error.&lt;code&gt;</c>.
/// </summary>
public static class E2eErrorCodes
{
    /// <summary>
    /// This device's keys are not unlocked.
    /// </summary>
    public const string Locked = "E2e.Locked";

    /// <summary>
    /// A file is encrypted and this device cannot decrypt it yet. Argument: the file's key.
    /// </summary>
    public const string CannotDecrypt = "E2e.CannotDecrypt";

    /// <summary>
    /// Encryption is on and this device cannot encrypt yet, so nothing can be saved.
    /// </summary>
    public const string CannotEncrypt = "E2e.CannotEncrypt";

    /// <summary>
    /// The passkey cannot unlock this device's keys; the passphrase is needed.
    /// </summary>
    public const string PassphraseRequired = "E2e.PassphraseRequired";

    /// <summary>
    /// The profile's keys were changed on another device (its passphrase was changed), so the passkey can no longer
    /// unlock it on this one; the new passphrase is needed.
    /// </summary>
    public const string KeysChanged = "E2e.KeysChanged";

    /// <summary>
    /// The signed in user has no protected profile with the given id ({0}).
    /// </summary>
    public const string ProfileNotFound = "E2e.ProfileNotFound";

    /// <summary>
    /// The new passphrase is shorter than the profile allows ({0}: the minimum length).
    /// </summary>
    public const string PassphraseTooShort = "E2e.PassphraseTooShort";

    /// <summary>
    /// The passphrase is not correct.
    /// </summary>
    public const string WrongPassphrase = "E2e.WrongPassphrase";

    /// <summary>
    /// The user's keys, or their list of protected profiles, kept changing on another device while this one tried to
    /// change them, so the change was not made.
    /// </summary>
    public const string DocumentChanged = "E2e.DocumentChanged";
}
