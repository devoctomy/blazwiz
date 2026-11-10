using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Implementation of <see cref="IE2eRotationSignOut"/>. The rotation mark is cleared by
/// <see cref="KeyRotationSignOutHandler"/>, after every session is revoked and before this device signs out.
/// </summary>
/// <param name="authService">Signs out everywhere.</param>
/// <param name="sessionLifecycle">Finishes signing out here: modules stop and forget what they hold.</param>
/// <param name="navigationService">Opens the login page.</param>
public class E2eRotationSignOut(
    IAuthenticationService authService,
    ISessionLifecycle sessionLifecycle,
    INavigationService navigationService)
    : IE2eRotationSignOut
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ISessionLifecycle sessionLifecycle = sessionLifecycle ?? throw new ArgumentNullException(nameof(sessionLifecycle));
    private readonly INavigationService navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

    /// <inheritdoc/>
    public async Task<bool> SignOutEverywhereAsync()
    {
        if (!await authService.SignOutEverywhereAsync(SignOutReason.KeysRotated))
        {
            return false;
        }

        await sessionLifecycle.SigningOutAsync();
        await sessionLifecycle.SignedOutAsync();
        navigationService.NavigateTo("/login");
        return true;
    }
}
