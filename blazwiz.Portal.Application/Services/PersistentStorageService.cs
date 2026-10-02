using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class PersistentStorageService(IStorageDurabilityService durabilityService, ILocalStorageService localStorageService)
    : IPersistentStorageService
{
    /// <summary>
    /// The local storage key the request state is kept under.
    /// </summary>
    public const string StorageKey = "blazwiz_storage_persistence";

    private readonly IStorageDurabilityService durabilityService = durabilityService ?? throw new ArgumentNullException(nameof(durabilityService));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task RequestAfterSignInAsync()
    {
        var status = await durabilityService.GetStatusAsync();
        if (!status.IsSupported || status.IsPersisted)
        {
            return;
        }

        var state = await localStorageService.GetObjectAsync<StoragePersistenceState>(StorageKey) ?? new StoragePersistenceState();
        var askAgain = !state.Granted && status.IsInstalled && !state.InstalledWhenRefused;
        if (state.Requested && !askAgain)
        {
            return;
        }

        var granted = await durabilityService.RequestPersistenceAsync();
        await localStorageService.SetObjectAsync(StorageKey, new StoragePersistenceState
        {
            Requested = true,
            Granted = granted,
            InstalledWhenRefused = !granted && status.IsInstalled,
        });
    }
}
