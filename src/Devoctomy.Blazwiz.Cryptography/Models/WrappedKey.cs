using System.Text.Json.Serialization;

namespace Devoctomy.Blazwiz.Cryptography.Models;

/// <summary>
/// A key encrypted ("wrapped") by another key, with what is needed to unwrap it. Binary values are base64.
/// </summary>
public class WrappedKey
{
    /// <summary>
    /// Gets or sets the algorithm it was wrapped with, e.g. <c>aes-256-gcm</c>.
    /// </summary>
    [JsonPropertyName("alg")]
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nonce, base64. New and random every time a key is wrapped.
    /// </summary>
    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped key followed by the authentication tag, base64.
    /// </summary>
    [JsonPropertyName("wrapped")]
    public string Wrapped { get; set; } = string.Empty;
}
