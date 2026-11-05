namespace Devoctomy.Blazwiz.Encryption;

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
    /// The start of the name of every key held for a key scope: <c>e2e:&lt;scope&gt;:master</c> for its raw master key and
    /// <c>e2e:&lt;scope&gt;:file:&lt;key id&gt;</c> for each raw file key, while unlocked.
    /// </summary>
    public const string ScopePrefix = "e2e:";

    /// <summary>
    /// Follows the scope id in the name of the scope's raw master key.
    /// </summary>
    public const string MasterKeySuffix = ":master";

    /// <summary>
    /// Follows the scope id in the name of each of the scope's raw file keys, and is followed by the key id.
    /// </summary>
    public const string FileKeySuffix = ":file:";
}
