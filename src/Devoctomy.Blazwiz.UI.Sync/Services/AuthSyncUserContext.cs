using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// The signed in user for sync: local files, the sync index and the sync log are partitioned by username.
/// </summary>
public class AuthSyncUserContext(IAuthenticationService authService)
    : ISyncUserContext
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));

    /// <inheritdoc/>
    public async Task<string?> GetUserIdAsync()
    {
        return (await authService.GetCurrentUserAsync())?.Username;
    }
}
