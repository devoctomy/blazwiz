using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Events.Models;

namespace Devoctomy.Blazwiz.Events.Channels.Interfaces;

/// <summary>
/// The signed-in user's event channel: every open instance of the app they use (tabs, devices) receives what the
/// others publish. Delivery is only to instances connected when an event is published: nothing is kept or replayed.
/// </summary>
public interface IUserEventChannel
{
    /// <summary>
    /// Raised when another instance's event arrives. Never raised for this instance's own events.
    /// </summary>
    event EventHandler<UserEvent>? Received;

    /// <summary>
    /// Raised when <see cref="State"/> changes, e.g. the connection dropped.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Gets this instance's id, made when it starts. Put in <see cref="UserEvent.From"/> so it can ignore its own events.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Gets whether events are being received.
    /// </summary>
    UserEventConnectionState State { get; }

    /// <summary>
    /// Connects to the user's channel. Does nothing if already connected.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ConnectAsync(UserSession session);

    /// <summary>
    /// Disconnects. Does nothing if not connected.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Publishes an event to the user's other instances, whether or not this one is connected.
    /// </summary>
    /// <param name="session">The signed-in user, whose channel it goes to.</param>
    /// <param name="userEvent">The event.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The session cannot publish (e.g. it has no tokens).</exception>
    Task PublishAsync(UserSession session, UserEvent userEvent);
}
