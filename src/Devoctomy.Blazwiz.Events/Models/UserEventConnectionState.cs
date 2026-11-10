namespace Devoctomy.Blazwiz.Events.Models;

/// <summary>
/// Whether a <see cref="Channels.Interfaces.IUserEventChannel"/> is receiving events.
/// </summary>
public enum UserEventConnectionState
{
    /// <summary>
    /// Not connected: events sent now are missed.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected and receiving events.
    /// </summary>
    Connected,
}
