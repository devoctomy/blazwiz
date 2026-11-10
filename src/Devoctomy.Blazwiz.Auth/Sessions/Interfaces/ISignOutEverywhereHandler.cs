using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

/// <summary>
/// Lets a module act when the user signs out on every device, after the sign-in service has revoked every session
/// and before this device signs out: e.g. tell the user's other open devices at once. Add one with
/// <see cref="Extensions.AuthServiceCollectionExtensions.AddSignOutEverywhereHandler{THandler}"/>. A handler that
/// throws does not stop the others or the sign out.
/// </summary>
public interface ISignOutEverywhereHandler
{
    /// <summary>
    /// Called while signing out everywhere, with this device still signed in.
    /// </summary>
    /// <param name="session">The session being signed out; its tokens may already be revoked with the sign-in service.</param>
    /// <param name="reason">Why.</param>
    /// <param name="deviceName">This device's name, to tell the others which device did it.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnSigningOutEverywhereAsync(UserSession session, SignOutReason reason, string deviceName);
}
