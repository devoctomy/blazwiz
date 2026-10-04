using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Authenticates users and tracks the current session.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Raised when <see cref="GetCurrentUserAsync"/> finds the session over (its token expired, or it could not be
    /// renewed) and has signed the user out, so the app can return to the login page. Not raised by
    /// <see cref="LogoutAsync"/>.
    /// </summary>
    event EventHandler? SessionEnded;

    /// <summary>
    /// Gets a value indicating whether this is the placeholder sign-in that accepts any credentials.
    /// </summary>
    bool IsPlaceholder { get; }

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
    /// Signs the current user out and clears their session.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task LogoutAsync();

    /// <summary>
    /// Gets the currently signed in user, or null if nobody is signed in or the session has expired.
    /// </summary>
    /// <returns>The current session, if any.</returns>
    Task<UserSession?> GetCurrentUserAsync();
}
