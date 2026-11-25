using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// Implementation of <see cref="ISavedDeviceCredentialService"/> over the users remembered on the login page
/// (<see cref="LoginViewModel.SavedUsersStorageKey"/>).
/// </summary>
/// <param name="authService">The signed in user.</param>
/// <param name="localStorageService">The remembered users.</param>
public class SavedDeviceCredentialService(IAuthenticationService authService, ILocalStorageService localStorageService)
    : ISavedDeviceCredentialService
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task DropAsync()
    {
        var session = await authService.GetCurrentUserAsync();
        if (session == null)
        {
            return;
        }

        var saved = await localStorageService.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey);
        var credential = saved?.Users.FirstOrDefault(u => u.IsFor(session.Username, session.Email))?.WebAuthnCredential;
        if (credential?.EncryptedDevice == null)
        {
            return;
        }

        credential.EncryptedDevice = null;
        await localStorageService.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, saved!);
    }
}
