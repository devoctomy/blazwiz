using System.Text.Json.Serialization;

namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A protected profile in the <see cref="E2eProfileIndex"/>.
/// </summary>
public class E2eProfileIndexEntry
{
    /// <summary>
    /// Gets or sets the profile's id: a GUID, which is also its key scope id.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the profile was created (UTC).
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }
}
