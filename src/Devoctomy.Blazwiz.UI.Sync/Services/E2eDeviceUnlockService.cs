using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Implementation of <see cref="IE2eDeviceUnlockService"/>.
/// </summary>
/// <param name="profiles">The user's profiles, which unlock with the passkey's key.</param>
/// <param name="manager">Says which are unlocked, and which have biometrics turned on here.</param>
/// <param name="passkeys">Asks for the passkey when its key is needed, and says whether there is one here.</param>
/// <param name="scheduler">Syncs a profile before it is unlocked.</param>
public class E2eDeviceUnlockService(
    ISyncProtectedProfileService profiles,
    IE2eProtectedProfileManager manager,
    IPasskeyKeysService passkeys,
    ISyncScheduler scheduler)
    : IE2eDeviceUnlockService
{
    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly IPasskeyKeysService passkeys = passkeys ?? throw new ArgumentNullException(nameof(passkeys));
    private readonly ISyncScheduler scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

    /// <inheritdoc/>
    public async Task<bool> CanUnlockAsync(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            return await manager.HasDeviceUnlockAsync(scope) && await passkeys.IsAvailableAsync();
        }
        catch (Exception)
        {
            // This device's copy of its keys cannot be read: the passphrase still unlocks it.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UnlockAsync(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (manager.IsUnlocked(scope))
        {
            return true;
        }

        await SyncFirstAsync(scope);
        try
        {
            if (!await passkeys.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey))
            {
                // Cancelled, or no passkey here.
                return false;
            }

            await profiles.UnlockWithDeviceAsync(scope);
            return true;
        }
        catch (Exception)
        {
            // E.g. another passkey than the one it was turned on with: the passphrase unlocks it.
            return false;
        }
    }

    /// <inheritdoc/>
    public Task ForgetAsync(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return profiles.DisableDeviceUnlockAsync(scope);
    }

    private async Task SyncFirstAsync(E2eKeyScope scope)
    {
        if (profiles.Profiles.FirstOrDefault(p => p.Scope == scope) is not { } profile)
        {
            return;
        }

        try
        {
            // Skipped offline or while sync is turned off; a failed pass is reported by the sync status and log.
            await scheduler.SyncProfileAsync(profile.Name);
        }
        catch (Exception)
        {
            // Unlocking goes ahead with the files this device has.
        }
    }
}
