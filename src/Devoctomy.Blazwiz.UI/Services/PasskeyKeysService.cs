using System.Security.Cryptography;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// Implementation of <see cref="IPasskeyKeysService"/> over the users remembered on the login page
/// (<see cref="LoginViewModel.SavedUsersStorageKey"/>) and the passkey PRF.
/// </summary>
/// <param name="authService">The signed in user.</param>
/// <param name="localStorageService">The remembered users.</param>
/// <param name="webAuthnService">Asks for the passkey, and derives the keys from it.</param>
/// <param name="keyStore">Holds the derived keys.</param>
public class PasskeyKeysService(
    IAuthenticationService authService,
    ILocalStorageService localStorageService,
    IWebAuthnService webAuthnService,
    IDerivedKeyStore keyStore)
    : IPasskeyKeysService
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IWebAuthnService webAuthnService = webAuthnService ?? throw new ArgumentNullException(nameof(webAuthnService));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync()
    {
        return await FindCredentialIdAsync() != null;
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureKeyAsync(string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        if (HasKey(keyName))
        {
            return true;
        }

        var credentialId = await FindCredentialIdAsync();
        if (credentialId == null)
        {
            return false;
        }

        try
        {
            var result = await webAuthnService.AuthenticateAsync(credentialId);
            return result.Success && result.KeysDerived && HasKey(keyName);
        }
        catch (Exception)
        {
            // Cancelled, or the browser refused: the caller falls back to the passphrase.
            return false;
        }
    }

    private bool HasKey(string keyName)
    {
        if (!keyStore.TryGetKey(keyName, out var key))
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(key);
        return true;
    }

    private async Task<string?> FindCredentialIdAsync()
    {
        var session = await authService.GetCurrentUserAsync();
        if (session == null)
        {
            return null;
        }

        SavedUsersData? saved;
        try
        {
            saved = await localStorageService.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey);
        }
        catch (Exception)
        {
            // Unreadable: nobody is remembered.
            return null;
        }

        var user = saved?.Users.FirstOrDefault(u => u.IsFor(session.Username, session.Email));
        return string.IsNullOrEmpty(user?.WebAuthnCredential?.CredentialID) ? null : user.WebAuthnCredential.CredentialID;
    }
}
