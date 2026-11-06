using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the Sync section of the Settings page: sync state, sync now, storage durability and moving the
/// files to another storage.
/// </summary>
public partial class SyncSettingsViewModel : ViewModelBase
{
    private readonly ISyncService syncService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncScheduler syncScheduler;
    private readonly ISyncStatusPresenter syncStatusPresenter;
    private readonly IStorageDurabilityService storageDurabilityService;
    private readonly ISyncStorageResolver storageResolver;
    private readonly ISyncStorageMigrationService storageMigrationService;
    private readonly ISyncFolderService folderService;
    private readonly ISyncProfileRegistry registry;

    private StorageDurabilityStatus? storage;
    private IReadOnlyList<string> switchableProfiles = [];
    private bool choosesDeviceDefault;
    private SyncStorageKind storageKind;
    private bool isOpfsSupported;
    private bool isFolderSupported;
    private string? folderName;
    private string? pendingFolderName;
    private bool isMigrating;
    private SyncStorageMigrationProgress? migrationProgress;
    private string? storageError;
    private SyncStorageKind? pendingStorageKind;
    private int pendingFileCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncSettingsViewModel"/> class.
    /// </summary>
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="syncStatusPresenter">Describes the sync status.</param>
    /// <param name="storageDurabilityService">Reports whether the browser keeps the site's storage.</param>
    /// <param name="storageResolver">Finds the storages the files can be kept in, and the one in use.</param>
    /// <param name="storageMigrationService">Moves the files to another storage.</param>
    /// <param name="folderService">Picks the folder on this computer for the files.</param>
    /// <param name="registry">The sync profiles: the storage choice is loaded again when one is added or removed.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public SyncSettingsViewModel(
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter,
        IStorageDurabilityService storageDurabilityService,
        ISyncStorageResolver storageResolver,
        ISyncStorageMigrationService storageMigrationService,
        ISyncFolderService folderService,
        ISyncProfileRegistry registry,
        IStringLocalizer<SyncUiText> text,
        IErrorText errorText)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.syncStatusPresenter = syncStatusPresenter ?? throw new ArgumentNullException(nameof(syncStatusPresenter));
        this.storageDurabilityService = storageDurabilityService ?? throw new ArgumentNullException(nameof(storageDurabilityService));
        this.storageResolver = storageResolver ?? throw new ArgumentNullException(nameof(storageResolver));
        this.storageMigrationService = storageMigrationService ?? throw new ArgumentNullException(nameof(storageMigrationService));
        this.folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
        this.registry.ProfileAdded += OnProfilesChanged;
        this.registry.ProfileRemoved += OnProfilesChanged;
    }

    /// <summary>
    /// Gets the sync state, as the status pill shows it.
    /// </summary>
    public SyncStatusPresentation SyncDisplay => syncStatusPresenter.Present(syncService.Status, connectivityService.Status);

    /// <summary>
    /// Gets the sync status: last synced time, pending changes and last error.
    /// </summary>
    public SyncStatus SyncStatus => syncService.Status;

    /// <summary>
    /// Gets the connectivity status and reason, e.g. "Offline: S3 unreachable".
    /// </summary>
    public string ConnectivityText => syncStatusPresenter.DescribeConnectivity(connectivityService.Status);

    /// <summary>
    /// Gets why the last sync failed, or null if it did not.
    /// </summary>
    public string? LastErrorText => syncService.Status.LastErrorCode is { } code
        ? errorText.Describe(code, syncService.Status.LastErrorArguments)
        : null;

    /// <summary>
    /// Gets the hint shown when the browser keeps storage on a best effort basis.
    /// </summary>
    public string InstallHint => text["Sync.Storage.InstallHint"];

    /// <summary>
    /// Gets a value indicating whether sync is turned on.
    /// </summary>
    public bool IsSyncEnabled => connectivityService.IsSyncEnabled;

    /// <summary>
    /// Gets a value indicating whether Sync now can be used.
    /// </summary>
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing && !IsMigrating;

    /// <summary>
    /// Gets a value indicating whether the files can be kept in more than one storage, so the choice is shown. With no
    /// profiles at all yet (e.g. an app whose profiles are all created by the user), the choice is the storage for the
    /// device, used by the profiles created later.
    /// </summary>
    public bool CanSwitchStorage => switchableProfiles.Count > 0 || choosesDeviceDefault;

    /// <summary>
    /// Gets the storage the files are kept in on this device.
    /// </summary>
    public SyncStorageKind StorageKind
    {
        get => storageKind;
        private set => SetProperty(ref storageKind, value);
    }

    /// <summary>
    /// Gets a value indicating whether the browser can keep files in OPFS.
    /// </summary>
    public bool IsOpfsSupported
    {
        get => isOpfsSupported;
        private set => SetProperty(ref isOpfsSupported, value);
    }

    /// <summary>
    /// Gets a value indicating whether the browser can keep files in a folder the user picks.
    /// </summary>
    public bool IsFolderSupported
    {
        get => isFolderSupported;
        private set => SetProperty(ref isFolderSupported, value);
    }

    /// <summary>
    /// Gets the name of the folder the files are kept in, when they are kept in one.
    /// </summary>
    public string? FolderName
    {
        get => folderName;
        private set => SetProperty(ref folderName, value);
    }

    /// <summary>
    /// Gets the name of the storage the user asked to move to, with the picked folder's name, e.g.
    /// "File System (Documents)".
    /// </summary>
    public string? PendingStorageName => PendingStorageKind is not SyncStorageKind kind
        ? null
        : kind == SyncStorageKind.LocalFolder && pendingFolderName != null
            ? text["Sync.Storage.KindWithFolder", GetStorageName(kind), pendingFolderName]
            : GetStorageName(kind);

    /// <summary>
    /// Gets a value indicating whether the files are being moved to another storage.
    /// </summary>
    public bool IsMigrating
    {
        get => isMigrating;
        private set
        {
            if (SetProperty(ref isMigrating, value))
            {
                OnPropertyChanged(nameof(CanSyncNow));
            }
        }
    }

    /// <summary>
    /// Gets the progress line while files are moved, e.g. "Syncing before moving…" or "Moving files: 3 of 10", or null.
    /// </summary>
    public string? MigrationText => migrationProgress switch
    {
        null => null,
        { IsSyncing: true } => text["Sync.Storage.SyncingBeforeMove"].Value,
        var progress => text["Sync.Storage.Moving", progress.Done, progress.Total].Value,
    };

    /// <summary>
    /// Gets why the last move failed, or null.
    /// </summary>
    public string? StorageError
    {
        get => storageError;
        private set => SetProperty(ref storageError, value);
    }

    /// <summary>
    /// Gets the storage the user asked to move to, while the confirmation is shown.
    /// </summary>
    public SyncStorageKind? PendingStorageKind
    {
        get => pendingStorageKind;
        private set
        {
            if (SetProperty(ref pendingStorageKind, value))
            {
                OnPropertyChanged(nameof(IsStorageDialogOpen));
                OnPropertyChanged(nameof(PendingStorageName));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the move confirmation is shown.
    /// </summary>
    public bool IsStorageDialogOpen => PendingStorageKind != null;

    /// <summary>
    /// Gets the number of files that will be moved.
    /// </summary>
    public int PendingFileCount
    {
        get => pendingFileCount;
        private set => SetProperty(ref pendingFileCount, value);
    }

    /// <summary>
    /// Gets a value indicating whether the storage status is known.
    /// </summary>
    public bool IsStorageKnown => storage is { IsSupported: true };

    /// <summary>
    /// Gets a value indicating whether the browser keeps the site's storage (persistent) rather than best effort.
    /// </summary>
    public bool IsStoragePersisted => storage?.IsPersisted == true;

    /// <summary>
    /// Gets the storage line, e.g. "Persistent · 2.1 MB used" or "Best effort · 2.1 MB used".
    /// </summary>
    public string StorageText
    {
        get
        {
            if (!IsStorageKnown)
            {
                return text["Sync.Storage.Unknown"];
            }

            string kind = IsStoragePersisted ? text["Sync.Storage.Persistent"] : text["Sync.Storage.BestEffort"];
            return storage!.UsageBytes is long usage ? text["Sync.Storage.Usage", kind, FormatSize(usage)] : kind;
        }
    }

    /// <summary>
    /// Gets a value indicating whether to suggest installing the app, because storage is only best effort.
    /// </summary>
    public bool ShowInstallHint => IsStorageKnown && !IsStoragePersisted && storage!.IsInstalled != true;

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await connectivityService.InitializeAsync();
        RefreshSync();
        try
        {
            storage = await storageDurabilityService.GetStatusAsync();
        }
        catch (Exception)
        {
            storage = null;
        }

        OnPropertyChanged(nameof(IsStorageKnown));
        OnPropertyChanged(nameof(IsStoragePersisted));
        OnPropertyChanged(nameof(StorageText));
        OnPropertyChanged(nameof(ShowInstallHint));
        await LoadStorageKindAsync();
    }

    /// <summary>
    /// Gets the name of a storage, as the settings show it.
    /// </summary>
    /// <param name="kind">The storage.</param>
    /// <returns>The name, e.g. "OPFS".</returns>
    public string GetStorageName(SyncStorageKind kind)
    {
        return kind switch
        {
            SyncStorageKind.IndexedDb => text["Sync.Storage.Kind.IndexedDb"],
            SyncStorageKind.Opfs => text["Sync.Storage.Kind.Opfs"],
            SyncStorageKind.LocalFolder => text["Sync.Storage.Kind.LocalFolder"],
            _ => kind.ToString(),
        };
    }

    /// <summary>
    /// Asks to move the files to another storage: for a folder, asks the user to pick one first (this follows the
    /// click, so the browser allows it); then counts the files and shows the confirmation.
    /// </summary>
    /// <param name="kind">The storage to move to.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task RequestStorageChangeAsync(SyncStorageKind kind)
    {
        if (kind == StorageKind || IsMigrating || !CanSwitchStorage)
        {
            return;
        }

        StorageError = null;
        if (kind == SyncStorageKind.LocalFolder)
        {
            try
            {
                pendingFolderName = await folderService.PickAsync();
            }
            catch (Exception ex)
            {
                StorageError = text["Sync.Storage.FolderOpenFailed", errorText.Describe(ex)];
                return;
            }

            if (pendingFolderName == null)
            {
                // Cancelled.
                return;
            }
        }

        if (switchableProfiles.Count == 0)
        {
            // No files yet, so nothing to move or confirm: just remember the choice for the profiles created later.
            try
            {
                await storageMigrationService.MigrateAsync(kind);
                StorageKind = kind;
                FolderName = kind == SyncStorageKind.LocalFolder ? pendingFolderName : null;
            }
            catch (Exception ex)
            {
                StorageError = text["Sync.Storage.MoveFailed", errorText.Describe(ex)];
            }

            return;
        }

        try
        {
            var count = 0;
            foreach (var profile in switchableProfiles)
            {
                var state = await storageResolver.GetStateAsync(profile);
                count += (await storageResolver.GetStore(profile, state.Kind).ListAsync()).Count;
            }

            PendingFileCount = count;
            PendingStorageKind = kind;
        }
        catch (Exception ex)
        {
            StorageError = text["Sync.Storage.ReadFailed", errorText.Describe(ex)];
        }
    }

    /// <summary>
    /// Closes the move confirmation without moving anything. Ignored while files are moving.
    /// </summary>
    [RelayCommand]
    public void CloseStorageDialog()
    {
        if (!IsMigrating)
        {
            PendingStorageKind = null;
        }
    }

    /// <summary>
    /// Moves the files to the storage asked for. Sync pauses while they move; if the move fails, the files stay
    /// where they were and the error is shown.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ConfirmStorageChangeAsync()
    {
        if (PendingStorageKind is not SyncStorageKind target || IsMigrating)
        {
            return;
        }

        IsMigrating = true;
        try
        {
            await storageMigrationService.MigrateAsync(target, new ProgressReporter(this));
            StorageKind = target;
            FolderName = target == SyncStorageKind.LocalFolder ? pendingFolderName : null;
        }
        catch (Exception ex)
        {
            StorageError = text["Sync.Storage.MoveFailed", errorText.Describe(ex)];
            await LoadStorageKindAsync();
        }
        finally
        {
            SetMigrationProgress(null);
            IsMigrating = false;
            PendingStorageKind = null;
            RefreshSync();
        }
    }

    /// <summary>
    /// Syncs every profile now.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SyncNowAsync()
    {
        if (CanSyncNow)
        {
            await syncScheduler.SyncNowAsync();
        }
    }

    /// <summary>
    /// Turns sync on or off. It is remembered until switched back.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleSyncAsync()
    {
        await connectivityService.SetSyncEnabledAsync(!connectivityService.IsSyncEnabled);
        RefreshSync();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            syncService.StatusChanged -= OnSyncStatusChanged;
            connectivityService.StatusChanged -= OnConnectivityChanged;
            registry.ProfileAdded -= OnProfilesChanged;
            registry.ProfileRemoved -= OnProfilesChanged;
        }

        base.Dispose(disposing);
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes} B")
            : string.Create(CultureInfo.CurrentCulture, $"{size:0.#} {units[unit]}");
    }

    private async Task LoadStorageKindAsync()
    {
        try
        {
            var profiles = syncService.Profiles;
            switchableProfiles = profiles.Where(storageResolver.IsSwitchable).ToList();
            choosesDeviceDefault = profiles.Count == 0;
            if (switchableProfiles.Count > 0)
            {
                StorageKind = (await storageResolver.GetStateAsync(switchableProfiles[0])).Kind;
            }
            else if (choosesDeviceDefault)
            {
                StorageKind = await storageResolver.GetDeviceDefaultKindAsync() ?? SyncStorageKind.IndexedDb;
            }

            if (CanSwitchStorage)
            {
                IsOpfsSupported = await storageResolver.IsSupportedAsync(SyncStorageKind.Opfs);
                IsFolderSupported = await storageResolver.IsSupportedAsync(SyncStorageKind.LocalFolder);
                FolderName = StorageKind == SyncStorageKind.LocalFolder ? (await folderService.GetStatusAsync())?.Name : null;
            }
        }
        catch (Exception)
        {
            switchableProfiles = [];
            choosesDeviceDefault = false;
        }

        OnPropertyChanged(nameof(CanSwitchStorage));
    }

    private void SetMigrationProgress(SyncStorageMigrationProgress? progress)
    {
        migrationProgress = progress;
        OnPropertyChanged(nameof(MigrationText));
    }

    private void RefreshSync()
    {
        OnPropertyChanged(nameof(SyncDisplay));
        OnPropertyChanged(nameof(SyncStatus));
        OnPropertyChanged(nameof(LastErrorText));
        OnPropertyChanged(nameof(ConnectivityText));
        OnPropertyChanged(nameof(IsSyncEnabled));
        OnPropertyChanged(nameof(CanSyncNow));
    }

    private async void OnProfilesChanged(object? sender, string profile)
    {
        // A profile created or deleted at runtime (e.g. a protected profile) changes which storages there are to choose
        // from: an app with no profiles in code has none until the user creates the first.
        try
        {
            await LoadStorageKindAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus e)
    {
        RefreshSync();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus e)
    {
        RefreshSync();
    }

    /// <summary>
    /// Shows move progress as it is reported, on the caller's context (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class ProgressReporter(SyncSettingsViewModel owner)
        : IProgress<SyncStorageMigrationProgress>
    {
        public void Report(SyncStorageMigrationProgress value)
        {
            owner.SetMigrationProgress(value);
        }
    }
}
