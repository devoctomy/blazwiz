namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Represents a stored WebAuthn credential associated with a saved user.
/// </summary>
public class WebAuthnStoredCredential
{
    /// <summary>
    /// Gets or sets the credential identifier.
    /// </summary>
    public string CredentialID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public string UserID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the credential was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the encrypted password (encrypted with the remember me key derived from the WebAuthn PRF output).
    /// </summary>
    public string? EncryptedPassword { get; set; }
}
