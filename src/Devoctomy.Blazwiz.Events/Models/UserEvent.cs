using System.Text.Json.Serialization;

namespace Devoctomy.Blazwiz.Events.Models;

/// <summary>
/// An event one of the user's devices sends the others on their channel. It only says that something happened, never
/// the change itself and never a secret: no tokens, keys or file names. A device that is closed or offline when it is
/// sent never gets it, so every event needs a fallback that works without it.
/// </summary>
public sealed class UserEvent
{
    /// <summary>
    /// Gets or sets what happened (see <see cref="UserEventTypes"/>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets why, when the type has reasons (see <see cref="UserEventReasons"/>).
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the name of the device that sent it, to show the user. It comes from another device: show it as
    /// plain text only.
    /// </summary>
    [JsonPropertyName("device")]
    public string? Device { get; set; }

    /// <summary>
    /// Gets or sets when it was sent.
    /// </summary>
    [JsonPropertyName("at")]
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets the sending instance's id (<see cref="Channels.Interfaces.IUserEventChannel.InstanceId"/>), so it
    /// ignores its own events.
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
}
