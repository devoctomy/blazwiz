using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Authenticates users and tracks the current session.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Attempts to sign in with the given credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The result of the login attempt.</returns>
    Task<LoginResult> LoginAsync(string username, string password);

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
