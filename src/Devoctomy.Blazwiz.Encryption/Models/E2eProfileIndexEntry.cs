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

    /// <summary>
    /// Gets or sets how long, in seconds, the profile stays unlocked without being used, chosen by the user for this
    /// profile on every device: 0 for never; null (not written) for the app's default.
    /// </summary>
    [JsonPropertyName("idleTimeoutSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IdleTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets the idle timeout chosen for this profile, if one was.
    /// </summary>
    /// <param name="idleTimeout">The idle timeout: null for never.</param>
    /// <returns>True if one was chosen; false to use the app's default.</returns>
    public bool TryGetIdleTimeout(out TimeSpan? idleTimeout)
    {
        idleTimeout = IdleTimeoutSeconds switch
        {
            null or 0 => null,
            { } seconds => TimeSpan.FromSeconds(seconds),
        };
        return IdleTimeoutSeconds != null;
    }
}
