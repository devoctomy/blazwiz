using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Connectivity;

/// <summary>
/// Remembers that sync is turned off in local storage, so it survives reloads and sign outs until it is turned on.
/// </summary>
public class LocalStorageSyncEnabledStore(ILocalStorageService localStorageService)
    : ISyncEnabledStore
{
    /// <summary>
    /// The local storage key set while sync is turned off.
    /// </summary>
    public const string StorageKey = "sync_disabled";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task<bool> GetAsync()
    {
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
