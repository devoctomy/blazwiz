namespace blazwiz.Portal.Models.WebAuthn;

/// <summary>
/// Represents the result of a WebAuthn registration operation.
/// </summary>
public class WebAuthnRegisterResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the registration was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the registration failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the credential identifier for the registered authenticator.
    /// </summary>
    public string? CredentialID { get; set; }

    /// <summary>
    /// Gets or sets the user identifier associated with the credential.
    /// </summary>
    public string? UserID { get; set; }

    /// <summary>
    /// Gets or sets the username associated with the credential.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Pseudo-Random Function (PRF) extension is enabled.
    /// </summary>
    public bool PrfEnabled { get; set; }

    /// <summary>
    /// Gets or sets the output from the Pseudo-Random Function (PRF) extension during registration.
    /// </summary>
    public string? PrfOutput { get; set; }
}
