using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;

namespace Devoctomy.Blazwiz.Events.Sessions;

/// <summary>
/// Tells the user's other open instances to end their sessions when they sign out everywhere, by publishing
/// <see cref="UserEventTypes.SignedOutEverywhere"/> with the reason and this device's name.
/// </summary>
/// <param name="channel">The user's event channel.</param>
/// <param name="timeProvider">The clock.</param>
public class UserEventsSignOutHandler(IUserEventChannel channel, TimeProvider timeProvider)
    : ISignOutEverywhereHandler
{
    private readonly IUserEventChannel channel = channel ?? throw new ArgumentNullException(nameof(channel));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public Task OnSigningOutEverywhereAsync(UserSession session, SignOutReason reason, string deviceName)
    {
        return channel.PublishAsync(session, new UserEvent
        {
            Type = UserEventTypes.SignedOutEverywhere,
            Reason = reason == SignOutReason.KeysRotated ? UserEventReasons.KeysRotated : UserEventReasons.UserRequested,
            Device = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName,
            At = timeProvider.GetUtcNow(),
            From = channel.InstanceId,
        });
    }
}
