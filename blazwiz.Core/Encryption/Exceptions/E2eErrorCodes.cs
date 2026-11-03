namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// The codes of end to end encryption errors users can see. The UI shows each with the text resource
/// <c>Error.&lt;code&gt;</c>.
/// </summary>
public static class E2eErrorCodes
{
    /// <summary>
    /// Encryption was already turned on from another device.
    /// </summary>
    public const string AlreadyEnabled = "E2e.AlreadyEnabled";

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
    /// The passphrase is not correct.
    /// </summary>
    public const string WrongPassphrase = "E2e.WrongPassphrase";
}
