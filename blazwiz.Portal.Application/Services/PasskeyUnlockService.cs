using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Implementation of <see cref="IPasskeyUnlockService"/> using the passkey saved with the user on the login page.
/// </summary>
public class PasskeyUnlockService(
    IAuthenticationService authService,
    ILocalStorageService localStorageService,
    IWebAuthnService webAuthnService)
    : IPasskeyUnlockService
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IWebAuthnService webAuthnService = webAuthnService ?? throw new ArgumentNullException(nameof(webAuthnService));

    /// <inheritdoc/>
    public async Task<bool> CanUnlockAsync()
    {
        return await GetCredentialIdAsync() != null;
    }

    /// <inheritdoc/>
    public async Task<bool> UnlockAsync()
    {
        var credentialId = await GetCredentialIdAsync();
        if (credentialId == null)
        {
            return false;
        }

        var result = await webAuthnService.AuthenticateAsync(credentialId);
        return result.Success && result.KeysDerived;
    }

    private async Task<string?> GetCredentialIdAsync()
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
            return null;
        }

        var user = saved?.Users.FirstOrDefault(u =>
            u.Email.Equals(session.Username, StringComparison.OrdinalIgnoreCase)
            || (session.Email != null && u.Email.Equals(session.Email, StringComparison.OrdinalIgnoreCase)));
        var credentialId = user?.WebAuthnCredential?.CredentialID;
        return string.IsNullOrEmpty(credentialId) ? null : credentialId;
    }
}
