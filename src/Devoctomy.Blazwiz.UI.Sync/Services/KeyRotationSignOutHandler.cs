using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Finishes a passphrase change while the user is signed out everywhere: every session is revoked by then, so the
/// rotation mark of each profile whose files this session encrypted again is cleared, before this device signs out.
/// If the sign out fails before this runs, the mark stays and the change is offered again until it succeeds.
/// </summary>
/// <remarks>
/// The rotation service needs the session (through sync and the user's keys), and the session needs this handler, so
/// the rotation service is resolved when a sign out runs, not when the handler is created.
/// </remarks>
/// <param name="services">Resolves the rotation service, which clears the rotation marks.</param>
public class KeyRotationSignOutHandler(IServiceProvider services)
    : ISignOutEverywhereHandler
{
    private readonly IServiceProvider services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc/>
    public Task OnSigningOutEverywhereAsync(UserSession session, SignOutReason reason, string deviceName)
    {
        return reason == SignOutReason.KeysRotated
            ? services.GetRequiredService<ISyncKeyRotationService>().CompleteAsync()
            : Task.CompletedTask;
    }
}
