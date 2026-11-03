using System.Security.Cryptography;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// Implementation of <see cref="IWebAuthnService"/> via browser JavaScript interop. The PRF output goes straight to the
/// <see cref="IPrfKeyDerivator"/> and is wiped, so nothing else in the app sees it.
/// </summary>
public class WebAuthnService(IJSRuntime jsRuntime, IPrfKeyDerivator prfKeyDerivator)
    : IWebAuthnService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IPrfKeyDerivator prfKeyDerivator = prfKeyDerivator ?? throw new ArgumentNullException(nameof(prfKeyDerivator));

    /// <inheritdoc/>
    public async Task<WebAuthnRegisterResult> RegisterAsync(string username, string? userId)
    {
        var result = await jsRuntime.InvokeAsync<JsRegisterResult>("webAuthnWrapper.register", username, userId);
        return new WebAuthnRegisterResult
        {
            Success = result.Success,
            Error = result.Error,
            CredentialID = result.CredentialID,
            UserID = result.UserID,
            Username = result.Username,
            PrfEnabled = result.PrfEnabled,
            KeysDerived = result.Success && DeriveKeys(result.PrfOutput),
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
            KeysDerived = result.Success && DeriveKeys(result.PrfOutput),
        };
    }

    private bool DeriveKeys(string? prfOutputHex)
    {
        if (string.IsNullOrEmpty(prfOutputHex))
        {
            return false;
        }

        var prfOutput = Convert.FromHexString(prfOutputHex);
        try
        {
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

        public string? PrfOutput { get; set; }
    }

    private sealed class JsAuthenticateResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public bool UserPresent { get; set; }

        public bool UserVerified { get; set; }

        public string? PrfOutput { get; set; }
    }
}
