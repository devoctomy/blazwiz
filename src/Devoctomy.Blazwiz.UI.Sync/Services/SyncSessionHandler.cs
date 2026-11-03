using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Starts and stops sync with the session: connectivity before the shell shows, then storage recovery, encryption,
/// the folder check and the scheduler in the background, and stopping the scheduler on sign out.
/// </summary>
public class SyncSessionHandler(
    IConnectivityService connectivityService,
    ISyncScheduler syncScheduler,
    ISyncStorageMigrationService storageMigrationService,
    ISyncStorageChannel storageChannel,
    ISyncEncryptionService encryptionService,
    ISyncFolderAccessMonitor folderAccessMonitor,
    IPersistentStorageService persistentStorageService)
    : ISessionLifecycleHandler
{
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly ISyncScheduler syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
    private readonly ISyncStorageMigrationService storageMigrationService = storageMigrationService ?? throw new ArgumentNullException(nameof(storageMigrationService));
    private readonly ISyncStorageChannel storageChannel = storageChannel ?? throw new ArgumentNullException(nameof(storageChannel));
    private readonly ISyncEncryptionService encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
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
            // Unlocks with this device's keys, or holds back encrypted profiles until it can.
            await encryptionService.InitializeAsync();
        }
        catch (Exception)
        {
            // The encryption status stays unknown, so encrypted profiles wait; it is tried again once online.
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
    }
}
