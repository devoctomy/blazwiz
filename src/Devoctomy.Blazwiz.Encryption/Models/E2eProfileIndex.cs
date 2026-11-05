using System.Text.Json.Serialization;

namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// The user's index of protected profiles (profiles with their own passphrase), kept in their
/// <see cref="Interfaces.IE2eProfileIndexStore"/>. It holds no keys and no names: only each profile's id.
/// </summary>
public class E2eProfileIndex
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
    /// Gets or sets the protected profiles, in the order they were created.
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<E2eProfileIndexEntry> Profiles { get; set; } = [];
}
