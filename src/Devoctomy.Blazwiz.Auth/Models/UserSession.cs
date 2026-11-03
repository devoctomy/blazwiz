namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Details of the currently signed in user.
/// </summary>
public class UserSession
{
    /// <summary>
    /// Gets or sets the username used to sign in.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the friendly display name of the user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the user, if known.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets when the user signed in.
    /// </summary>
    public DateTimeOffset SignedInAt { get; set; }

    /// <summary>
    /// Gets or sets when the session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the Cognito ID token, when signed in with Cognito.
    /// </summary>
    public string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the Cognito access token, when signed in with Cognito.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an offline session: no tokens, held in memory only, and ended
    /// at <see cref="ExpiresAt"/> or when offline mode is turned off.
    /// </summary>
    public bool IsOffline { get; set; }
}
