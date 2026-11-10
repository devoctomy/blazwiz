using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

/// <summary>
/// Calls every <see cref="ISignOutEverywhereHandler"/>. Used by the authentication services.
/// </summary>
public interface ISignOutEverywhereNotifier
{
    /// <summary>
    /// Gets a value indicating whether any handler is registered, i.e. other open devices can be told at once. Without
    /// one, signing out everywhere still revokes every session, but other devices only find out when they next use it.
    /// </summary>
    bool HasHandlers { get; }

    /// <summary>
    /// Calls <see cref="ISignOutEverywhereHandler.OnSigningOutEverywhereAsync"/> on every handler, with this device's
    /// name.
    /// </summary>
    /// <param name="session">The session being signed out.</param>
    /// <param name="reason">Why the user is signed out everywhere.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task NotifyAsync(UserSession session, SignOutReason reason);
}
