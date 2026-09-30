namespace blazwiz.Portal.Models.Auth;

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
}
