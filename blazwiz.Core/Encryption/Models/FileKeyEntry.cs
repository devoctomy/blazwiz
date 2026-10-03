using System.Text.Json.Serialization;
using blazwiz.Core.Cryptography.Models;

namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// A file key in a <see cref="UserKeysDocument"/>, wrapped by the master key with its id as associated data.
/// </summary>
public class FileKeyEntry : WrappedKey
{
    /// <summary>
    /// The status of the key new files are encrypted with. Only one key is active at a time.
    /// </summary>
    public const string ActiveStatus = "active";

    /// <summary>
    /// The status of a key kept only to decrypt files encrypted before it was replaced.
    /// </summary>
    public const string RetiredStatus = "retired";

    /// <summary>
    /// Gets or sets when the key was made (UTC).
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the status: <see cref="ActiveStatus"/> or <see cref="RetiredStatus"/>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = ActiveStatus;
}
