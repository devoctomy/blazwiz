namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Represents a saved user for quick login.
/// </summary>
public class SavedUser
{
    /// <summary>
    /// Gets or sets the email address of the saved user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the WebAuthn credential associated with this user, if any.
    /// </summary>
    public WebAuthnStoredCredential? WebAuthnCredential { get; set; }

    /// <summary>
    /// Gets whether this is the saved user for a username or email address, compared without regard to case. The one
    /// rule for matching a saved user, wherever a session or a typed address is looked up.
    /// </summary>
    /// <param name="username">The username, usually the email address.</param>
    /// <param name="email">The email address, when known separately from the username.</param>
    /// <returns>True when either matches the saved email address.</returns>
    public bool IsFor(string? username, string? email = null)
    {
        return (username != null && string.Equals(Email, username.Trim(), StringComparison.OrdinalIgnoreCase))
            || (email != null && string.Equals(Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
