using blazwiz.Portal.Models.WebAuthn;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Provides WebAuthn authentication functionality via browser JavaScript interop. When the authenticator supports the
/// PRF extension, the configured keys are derived from its output into the <c>IDerivedKeyStore</c>; the PRF output
/// itself is never returned.
/// </summary>
public interface IWebAuthnService
{
    /// <summary>
    /// Registers a new WebAuthn credential for the specified user.
    /// </summary>
    /// <param name="username">The username to register the credential for.</param>
    /// <param name="userId">The user identifier, or null to generate one automatically.</param>
    /// <returns>The result of the registration operation, saying whether keys were derived.</returns>
    Task<WebAuthnRegisterResult> RegisterAsync(string username, string? userId);

    /// <summary>
    /// Authenticates a user using an existing WebAuthn credential.
    /// </summary>
    /// <param name="credentialId">The credential identifier to authenticate with.</param>
    /// <returns>The result of the authentication operation, saying whether keys were derived.</returns>
    Task<WebAuthnAuthenticateResult> AuthenticateAsync(string credentialId);
}
