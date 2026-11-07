using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Implementation of <see cref="IE2eProfilePresenter"/>.
/// </summary>
/// <param name="profiles">The user's profiles.</param>
/// <param name="manager">Says which are unlocked, and which the passkey can unlock.</param>
/// <param name="displayNames">The names profiles are shown by.</param>
/// <param name="passkeys">Whether the signed in user has a passkey on this device.</param>
public class E2eProfilePresenter(
    ISyncProtectedProfileService profiles,
    IE2eProtectedProfileManager manager,
    ISyncProfileDisplayNames displayNames,
    IPasskeyKeysService passkeys)
    : IE2eProfilePresenter
{
    private readonly IPasskeyKeysService passkeys = passkeys ?? throw new ArgumentNullException(nameof(passkeys));
    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly ISyncProfileDisplayNames displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<E2eProfileRow>> GetProfilesAsync()
    {
        var rows = new List<E2eProfileRow>();
        foreach (var profile in profiles.Profiles)
        {
            rows.Add(await DescribeAsync(profile));
        }

        return rows;
    }

    /// <inheritdoc/>
    public async Task<E2eProfileRow?> GetProfileAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return profiles.Profiles.FirstOrDefault(p => p.Name == name) is { } profile ? await DescribeAsync(profile) : null;
    }

    private async Task<E2eProfileRow> DescribeAsync(SyncProtectedProfile profile)
    {
        var state = !profile.IsSetUp ? E2eProfileState.NotSetUp
            : manager.IsUnlocked(profile.Scope) ? E2eProfileState.Unlocked
            : E2eProfileState.Locked;
        var offered = profile.Settings.AllowDeviceUnlock && state != E2eProfileState.NotSetUp && await passkeys.IsAvailableAsync();
        var isOn = offered && await HasDeviceUnlockAsync(profile);
        return new E2eProfileRow
        {
            Name = profile.Name,
            Id = profile.Scope.Id,
            DisplayName = displayNames.GetDisplayName(profile.Name),
            State = state,
            IsCreatedByUser = profile.IsCreatedByUser,
            AllowDeviceUnlock = profile.Settings.AllowDeviceUnlock,
            CanUnlockWithDevice = isOn,
            CanSetUpDeviceUnlock = offered && !isOn,
        };
    }

    private async Task<bool> HasDeviceUnlockAsync(SyncProtectedProfile profile)
    {
        try
        {
            return await manager.HasDeviceUnlockAsync(profile.Scope);
        }
        catch (Exception)
        {
            // This device's copy of the keys cannot be read: the passphrase still unlocks it.
            return false;
        }
    }
}
