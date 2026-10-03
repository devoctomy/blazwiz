using System.Text.Json.Serialization;
using blazwiz.Core.Cryptography.Models;

namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// The user's keys document (keys.json), kept in their <see cref="Cryptography.Interfaces.IUserKeysStore"/> and never
/// with the synced files. Nothing in it is usable without the passphrase, but losing it means losing every encrypted
/// file.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Kdf"/>: everything needed to derive the passphrase key from the passphrase again.</item>
/// <item><see cref="MasterKey"/>: the master key, wrapped by the passphrase key. A failed unwrap means a wrong passphrase.</item>
/// <item><see cref="Keys"/>: the file keys by id, each wrapped by the master key with its id as associated data, so
/// entries cannot be swapped.</item>
/// </list>
/// </remarks>
public class UserKeysDocument
{
    /// <summary>
    /// The only format version so far.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Gets or sets the format version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Gets or sets how the passphrase key is derived.
    /// </summary>
    [JsonPropertyName("kdf")]
    public KdfSettings Kdf { get; set; } = new KdfSettings();

    /// <summary>
    /// Gets or sets the master key, wrapped by the passphrase key.
    /// </summary>
    [JsonPropertyName("master_key")]
    public WrappedKey MasterKey { get; set; } = new WrappedKey();

    /// <summary>
    /// Gets or sets the file keys by id, each wrapped by the master key.
    /// </summary>
    [JsonPropertyName("keys")]
    public Dictionary<string, FileKeyEntry> Keys { get; set; } = new Dictionary<string, FileKeyEntry>(StringComparer.Ordinal);
}
