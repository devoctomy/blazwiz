using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Core.Offline.Models;

namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Authenticates users and tracks the current session.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Raised when the session ended without the user signing out on this device, and the user has been signed out
    /// here: <see cref="GetCurrentUserAsync"/> found it over (its token expired, or it could not be renewed),
    /// <see cref="CheckSessionAsync"/> found it revoked, or <see cref="EndSessionAsync"/> was called because another
    /// device signed out everywhere. The app returns to the login page and can say why. Not raised by
    /// <see cref="LogoutAsync"/> or <see cref="SignOutEverywhereAsync"/>.
    /// </summary>
    event EventHandler<SessionEndedEventArgs>? SessionEnded;

    /// <summary>
    /// Gets a value indicating whether this is the placeholder sign-in that accepts any credentials.
    /// </summary>
    bool IsPlaceholder { get; }

    /// <summary>
    /// Gets how long before the session's token expires <see cref="GetCurrentUserAsync"/> refreshes it (or ends the
    /// session when it cannot be refreshed), so reading the session then has side effects. Zero when reading never does.
    /// </summary>
    TimeSpan RefreshMargin { get; }

    /// <summary>
    /// Attempts to sign in with the given credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>
    /// The result of the login attempt. When <see cref="LoginResult.NewPasswordRequired"/> is set, call
    /// <see cref="CompleteNewPasswordAsync"/> to finish signing in.
    /// </returns>
    Task<LoginResult> LoginAsync(string username, string password);

    /// <summary>
    /// Finishes a sign-in that needs a new password (e.g. the user was invited with a temporary one).
    /// </summary>
    /// <param name="newPassword">The new password.</param>
    /// <returns>The result of the login attempt.</returns>
    Task<LoginResult> CompleteNewPasswordAsync(string newPassword);

    /// <summary>
    /// Signs in offline, without the sign-in service, and turns offline mode on. Call only once the user has proved
    /// who they are on this device: their passkey decrypted the password remembered for them. Any online session is
    /// dropped.
    /// </summary>
    /// <param name="username">The remembered user's email address.</param>
    /// <param name="reason">Why offline: chosen, or the sign-in service could not be reached.</param>
    /// <returns>The result, with the offline session.</returns>
    Task<LoginResult> SignInOfflineAsync(string username, OfflineEntryReason reason);

    /// <summary>
    /// Turns offline mode on for the signed-in user: the session becomes an offline one lasting
    /// <see cref="Core.Offline.OfflineModeService.SessionLength"/> from now, and the online session is ended (its
    /// refresh token revoked first, while still online).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
    Task GoOfflineAsync();

    /// <summary>
    /// Signs the current user out and clears their session.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task LogoutAsync();

    /// <summary>
    /// Gets the currently signed in user, or null if nobody is signed in or the session has expired.
    /// </summary>
    /// <returns>The current session, if any.</returns>
    Task<UserSession?> GetCurrentUserAsync();

    /// <summary>
    /// Signs the user out on every device: the sign-in service revokes every session they have, every
    /// <see cref="Sessions.Interfaces.ISignOutEverywhereHandler"/> runs (e.g. to tell their other open devices at
    /// once), then they are signed out here as by <see cref="LogoutAsync"/>. Other devices that are not told find out
    /// the next time they use their session (see <see cref="CheckSessionAsync"/>). Needs the sign-in service: when it
    /// cannot be reached, or in offline mode, nothing changes, since a sign out here that pretends to be everywhere
    /// would be worse than an error.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>True if the user was signed out everywhere; false if nothing changed.</returns>
    Task<bool> SignOutEverywhereAsync(SignOutReason reason);

    /// <summary>
    /// Ends the session on this device because of something that happened elsewhere (e.g. another device signed the
    /// user out everywhere): signs out as <see cref="LogoutAsync"/> does, then raises <see cref="SessionEnded"/>.
    /// </summary>
    /// <param name="reason">Why the session ended.</param>
    /// <param name="deviceName">The name of the device that ended it, if known.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task EndSessionAsync(SessionEndedReason reason, string? deviceName);

    /// <summary>
    /// Checks the session with the sign-in service, e.g. when the app resumes after being closed, offline or asleep.
    /// When the service refuses it (the user signed out everywhere meanwhile), the session ends with
    /// <see cref="SessionEndedReason.Revoked"/>. When the service cannot be reached, or in offline mode, the session
    /// is kept: it is checked again next time.
    /// </summary>
    /// <returns>True if someone is still signed in; false if not, or the session has just ended.</returns>
    Task<bool> CheckSessionAsync();
}
