using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Remembers offline mode in local storage, so it survives reloads and sign outs until it is switched off.
/// </summary>
public class LocalStorageOfflineModeStore(ILocalStorageService localStorageService)
    : IOfflineModeStore
{
    /// <summary>
    /// The local storage key offline mode is kept under.
    /// </summary>
    public const string StorageKey = "blazwiz_offline_mode";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task<bool> GetAsync()
    {
        return await localStorageService.GetItemAsync(StorageKey) == bool.TrueString;
    }

    /// <inheritdoc/>
    public async Task SetAsync(bool forcedOffline)
    {
        if (forcedOffline)
        {
            await localStorageService.SetItemAsync(StorageKey, bool.TrueString);
        }
        else
        {
            await localStorageService.RemoveItemAsync(StorageKey);
        }
    }
}
