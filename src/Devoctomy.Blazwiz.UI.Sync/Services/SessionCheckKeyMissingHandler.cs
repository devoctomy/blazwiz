using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Checks the session when an unlocked profile meets a file under a key it does not know: the keys were most likely
/// rotated on another device, which signed every device out. If the sign-in service refuses the session, it ends and
/// the app returns to the login page; otherwise nothing changes.
/// </summary>
/// <param name="authService">Checks the session.</param>
public class SessionCheckKeyMissingHandler(IAuthenticationService authService)
    : ISyncKeyMissingHandler
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));

    /// <inheritdoc/>
    public Task OnKeyMissingAsync(E2eKeyScope scope, string keyId)
    {
        return authService.CheckSessionAsync();
    }
}
