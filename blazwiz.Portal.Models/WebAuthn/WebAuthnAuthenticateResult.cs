namespace blazwiz.Portal.Models.WebAuthn;

/// <summary>
/// Represents the result of a WebAuthn authentication operation.
/// </summary>
public class WebAuthnAuthenticateResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the authentication was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the authentication failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user presence was confirmed during authentication.
    /// </summary>
    public bool UserPresent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user was verified (e.g., with biometrics or PIN) during authentication.
    /// </summary>
    public bool UserVerified { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether keys were derived from the Pseudo-Random Function (PRF) output and put in
    /// the key store. The PRF output itself never leaves the WebAuthn service.
    /// </summary>
    public bool KeysDerived { get; set; }
}
