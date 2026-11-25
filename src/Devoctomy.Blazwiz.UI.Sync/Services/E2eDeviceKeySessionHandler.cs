using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Wipes the key derived from the passkey that unwraps the profiles' keys on this device
/// (<see cref="E2eKeyNames.KeyEncryptionKey"/>) once the session has started. Signing in with the passkey derives it,
/// but nothing at sign in needs it: unlocking a profile with biometrics asks for the passkey, and wipes the key again
/// straight after.
/// </summary>
/// <param name="passkeys">Wipes the key.</param>
public class E2eDeviceKeySessionHandler(IPasskeyKeysService passkeys)
    : ISessionLifecycleHandler
{
    private readonly IPasskeyKeysService passkeys = passkeys ?? throw new ArgumentNullException(nameof(passkeys));

    /// <inheritdoc/>
    public Task OnSessionStartedAsync(UserSession session)
    {
        passkeys.ForgetKey(E2eKeyNames.KeyEncryptionKey);
        return Task.CompletedTask;
    }
}
