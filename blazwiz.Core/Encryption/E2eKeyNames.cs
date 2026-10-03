namespace blazwiz.Core.Encryption;

/// <summary>
/// The names end to end encryption keys are held under in the <see cref="Cryptography.Interfaces.IDerivedKeyStore"/>.
/// </summary>
public static class E2eKeyNames
{
    /// <summary>
    /// The key encryption key derived from the passkey PRF. Wraps the master key for this device only. Add it to the
    /// keys derived from the PRF with <c>AddKey(E2eKeyNames.KeyEncryptionKey)</c>.
    /// </summary>
    public const string KeyEncryptionKey = "e2e-kek";

    /// <summary>
    /// The raw master key, while unlocked.
    /// </summary>
    public const string MasterKey = "e2e-master";

    /// <summary>
    /// The prefix of each raw file key, followed by its id, while unlocked.
    /// </summary>
    public const string FileKeyPrefix = "e2e-file:";
}
