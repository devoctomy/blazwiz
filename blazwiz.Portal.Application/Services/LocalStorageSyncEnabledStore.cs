using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Remembers that sync is turned off in local storage, so it survives reloads and sign outs until it is turned on.
/// </summary>
public class LocalStorageSyncEnabledStore(ILocalStorageService localStorageService)
    : ISyncEnabledStore
{
    /// <summary>
    /// The local storage key set while sync is turned off.
    /// </summary>
    public const string StorageKey = "blazwiz_sync_disabled";

    /// <summary>
    /// The key the earlier "offline mode" setting was kept under, which turned sync off. It is carried over once.
    /// </summary>
    public const string LegacyStorageKey = "blazwiz_offline_mode";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task<bool> GetAsync()
    {
        if (await localStorageService.GetItemAsync(LegacyStorageKey) == bool.TrueString)
        {
            await localStorageService.SetItemAsync(StorageKey, bool.TrueString);
            await localStorageService.RemoveItemAsync(LegacyStorageKey);
            return false;
        }

        return await localStorageService.GetItemAsync(StorageKey) != bool.TrueString;
    }

    /// <inheritdoc/>
    public async Task SetAsync(bool enabled)
    {
        if (enabled)
        {
            await localStorageService.RemoveItemAsync(StorageKey);
        }
        else
        {
            await localStorageService.SetItemAsync(StorageKey, bool.TrueString);
        }
    }
}
