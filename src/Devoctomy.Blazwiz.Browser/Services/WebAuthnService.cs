using System.Security.Cryptography;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// Implementation of <see cref="IWebAuthnService"/> via browser JavaScript interop. The PRF output arrives as bytes,
/// goes straight to the <see cref="IPrfKeyDerivator"/> and is wiped, so nothing else in the app sees it. Keys are only
/// derived when the authenticator verified the user (fingerprint, face or PIN): a touch alone proves possession, not
/// the user.
/// </summary>
public class WebAuthnService(IJSRuntime jsRuntime, IPrfKeyDerivator prfKeyDerivator)
    : IWebAuthnService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IPrfKeyDerivator prfKeyDerivator = prfKeyDerivator ?? throw new ArgumentNullException(nameof(prfKeyDerivator));

    /// <inheritdoc/>
    public async Task<WebAuthnRegisterResult> RegisterAsync(string username)
    {
        var result = await jsRuntime.InvokeAsync<JsRegisterResult>("webAuthnWrapper.register", username);
        return new WebAuthnRegisterResult
        {
            Success = result.Success,
            Error = result.Error,
            CredentialID = result.CredentialID,
            UserID = result.UserID,
            Username = result.Username,
            PrfEnabled = result.PrfEnabled,
            UserVerified = result.UserVerified,
            KeysDerived = DeriveKeys(result.Success && result.UserVerified, result.PrfOutput),
        };
    }

    /// <inheritdoc/>
    public async Task<WebAuthnAuthenticateResult> AuthenticateAsync(string credentialId)
    {
        var result = await jsRuntime.InvokeAsync<JsAuthenticateResult>("webAuthnWrapper.authenticate", credentialId);
        return new WebAuthnAuthenticateResult
        {
            Success = result.Success,
            Error = result.Error,
            UserPresent = result.UserPresent,
            UserVerified = result.UserVerified,
            KeysDerived = DeriveKeys(result.Success && result.UserVerified, result.PrfOutput),
        };
    }

    // Derives the keys when allowed; the PRF output is wiped either way.
    private bool DeriveKeys(bool allowed, byte[]? prfOutput)
    {
        if (prfOutput == null || prfOutput.Length == 0)
        {
            return false;
        }

        try
        {
            if (!allowed)
            {
                return false;
            }

            prfKeyDerivator.DeriveKeys(prfOutput);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prfOutput);
        }
    }

    private sealed class JsRegisterResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public string? CredentialID { get; set; }

        public string? UserID { get; set; }

        public string? Username { get; set; }

        public bool PrfEnabled { get; set; }

        public bool UserVerified { get; set; }

        public byte[]? PrfOutput { get; set; }
    }

    private sealed class JsAuthenticateResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public bool UserPresent { get; set; }

        public bool UserVerified { get; set; }

        public byte[]? PrfOutput { get; set; }
    }
}
