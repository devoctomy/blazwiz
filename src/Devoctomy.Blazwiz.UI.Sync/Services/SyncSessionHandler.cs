using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Starts and stops sync with the session: connectivity before the shell shows, then storage recovery, the user's
/// profiles and their passphrase status, the folder check and the scheduler in the background; on sign out, stopping
/// the scheduler and locking every profile.
/// </summary>
public class SyncSessionHandler(
    IConnectivityService connectivityService,
    ISyncScheduler syncScheduler,
    ISyncStorageMigrationService storageMigrationService,
    ISyncStorageChannel storageChannel,
    ISyncProtectedProfileService protectedProfiles,
    ISyncFolderAccessMonitor folderAccessMonitor,
    IPersistentStorageService persistentStorageService)
    : ISessionLifecycleHandler
{
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly ISyncScheduler syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
    private readonly ISyncStorageMigrationService storageMigrationService = storageMigrationService ?? throw new ArgumentNullException(nameof(storageMigrationService));
    private readonly ISyncStorageChannel storageChannel = storageChannel ?? throw new ArgumentNullException(nameof(storageChannel));
    private readonly ISyncProtectedProfileService protectedProfiles = protectedProfiles ?? throw new ArgumentNullException(nameof(protectedProfiles));
    private readonly ISyncFolderAccessMonitor folderAccessMonitor = folderAccessMonitor ?? throw new ArgumentNullException(nameof(folderAccessMonitor));
    private readonly IPersistentStorageService persistentStorageService = persistentStorageService ?? throw new ArgumentNullException(nameof(persistentStorageService));

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public Task OnSignedInAsync()
    {
        // Straight after the sign-in click, so Firefox's permission prompt appears at a sensible moment.
        return persistentStorageService.RequestAfterSignInAsync();
    }

    /// <inheritdoc/>
    public Task OnShellLoadingAsync(UserSession session)
    {
        return connectivityService.InitializeAsync();
    }

    /// <inheritdoc/>
    public async Task OnSessionStartedAsync(UserSession session)
    {
        try
        {
            await storageChannel.StartAsync();

            // A storage move cut short (e.g. by a reload) is finished or undone before anything syncs.
            await storageMigrationService.RecoverAsync();
        }
        catch (Exception)
        {
            // The files stay where they are; sync still runs against the storage in use.
        }

        try
        {
            // Every profile is locked until the user unlocks it; those the user created are added as profiles.
            await protectedProfiles.LoadAsync();
        }
        catch (Exception)
        {
            // The keys or the index cannot be read: the profiles stay locked, and their files keep syncing encrypted.
        }

        folderAccessMonitor.Start();
        if (await folderAccessMonitor.CheckAsync())
        {
            // The folder banner asks the user to reconnect; sync starts once they do.
            return;
        }

        try
        {
            await syncScheduler.StartAsync();
        }
        catch (Exception)
        {
            // Sync failures show in the status pill and the sync log; the app keeps working locally.
        }
    }

    /// <inheritdoc/>
    public async Task OnSigningOutAsync()
    {
        // Stop syncing first. Local data stays, partitioned by user.
        folderAccessMonitor.Stop();
        await syncScheduler.StopAsync();

        // Their keys are wiped now, not when the sign-in page next loads.
        protectedProfiles.Unload();
    }
}
