//#if (IncludeNotes)
using System.Globalization;
//#endif
using Blazing.Mvvm.ComponentModel;
//#if (IncludeNotes)
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.Storage;
//#endif
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;
//#if (IncludeNotes)
using blazwiz.Portal.Models.Sync;
//#endif
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
//#if (IncludeNotes)
    /// <summary>
    /// The hint shown when the browser keeps storage on a best effort basis.
    /// </summary>
    public const string InstallHint = "Install the app so your browser keeps your data";

//#endif
    private readonly IThemeService themeService;
//#if (IncludeNotes)
    private readonly ISyncService syncService;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncScheduler syncScheduler;
    private readonly ISyncStatusPresenter syncStatusPresenter;
    private readonly IStorageDurabilityService storageDurabilityService;
    private readonly ISyncStorageResolver storageResolver;
    private readonly ISyncStorageMigrationService storageMigrationService;

    private StorageDurabilityStatus? storage;
    private IReadOnlyList<string> switchableProfiles = [];
    private SyncStorageKind storageKind;
    private bool isOpfsSupported;
    private bool isMigrating;
    private SyncStorageMigrationProgress? migrationProgress;
    private string? storageError;
    private SyncStorageKind? pendingStorageKind;
    private int pendingFileCount;
//#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    ///#if (IncludeNotes)
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="syncStatusPresenter">Describes the sync status.</param>
    /// <param name="storageDurabilityService">Reports whether the browser keeps the site's storage.</param>
    /// <param name="storageResolver">Finds the storages the files can be kept in, and the one in use.</param>
    /// <param name="storageMigrationService">Moves the files to another storage.</param>
    ///#endif
    /// <param name="themeService">The theme service.</param>
    public SettingsViewModel(
//#if (IncludeNotes)
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter,
        IStorageDurabilityService storageDurabilityService,
        ISyncStorageResolver storageResolver,
        ISyncStorageMigrationService storageMigrationService,
//#endif
        IThemeService themeService)
    {
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
//#if (IncludeNotes)
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.syncStatusPresenter = syncStatusPresenter ?? throw new ArgumentNullException(nameof(syncStatusPresenter));
        this.storageDurabilityService = storageDurabilityService ?? throw new ArgumentNullException(nameof(storageDurabilityService));
        this.storageResolver = storageResolver ?? throw new ArgumentNullException(nameof(storageResolver));
        this.storageMigrationService = storageMigrationService ?? throw new ArgumentNullException(nameof(storageMigrationService));
//#endif
        this.themeService.ThemeChanged += OnThemeChanged;
//#if (IncludeNotes)
        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
//#endif
    }

    /// <summary>
    /// Gets the current theme name.
    /// </summary>
    public string CurrentTheme => themeService.CurrentTheme;

//#if (IncludeNotes)
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
    public string ConnectivityText => connectivityService.Status.Describe();

    /// <summary>
    /// Gets a value indicating whether offline mode is on.
    /// </summary>
    public bool IsForcedOffline => connectivityService.IsForcedOffline;

    /// <summary>
    /// Gets a value indicating whether Sync now can be used.
    /// </summary>
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing && !IsMigrating;

    /// <summary>
    /// Gets a value indicating whether the files can be kept in more than one storage, so the choice is shown.
    /// </summary>
    public bool CanSwitchStorage => switchableProfiles.Count > 0;

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
    /// Gets the progress line while files are moved, e.g. "Moving files: 3 of 10", or null.
    /// </summary>
    public string? MigrationText => migrationProgress is { } progress
        ? string.Create(CultureInfo.InvariantCulture, $"Moving files: {progress.Done} of {progress.Total}")
        : null;

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
                return "Unknown";
            }

            var kind = IsStoragePersisted ? "Persistent" : "Best effort";
            return storage!.UsageBytes is long usage ? $"{kind} · {FormatSize(usage)} used" : kind;
        }
    }

    /// <summary>
    /// Gets a value indicating whether to suggest installing the app, because storage is only best effort.
    /// </summary>
    public bool ShowInstallHint => IsStorageKnown && !IsStoragePersisted && storage!.IsInstalled != true;

//#endif
    /// <summary>
    /// Selects the given theme.
    /// </summary>
    /// <param name="theme">The theme name, see <see cref="ThemeNames"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task SelectThemeAsync(string theme)
    {
        return themeService.SetThemeAsync(theme);
    }

//#if (IncludeNotes)
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
    /// <returns>The name, e.g. "Browser file system (OPFS)".</returns>
    public string GetStorageName(SyncStorageKind kind)
    {
        return kind switch
        {
            SyncStorageKind.IndexedDb => "Browser database (IndexedDB)",
            SyncStorageKind.Opfs => "Browser file system (OPFS)",
            _ => kind.ToString(),
        };
    }

    /// <summary>
    /// Asks to move the files to another storage: counts them and shows the confirmation.
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
            StorageError = $"Your files could not be read: {ex.Message}";
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
        }
        catch (Exception ex)
        {
            StorageError = $"Your files could not be moved: {ex.Message}";
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
    /// Turns offline mode on or off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleOfflineModeAsync()
    {
        await connectivityService.SetForcedOfflineAsync(!connectivityService.IsForcedOffline);
        RefreshSync();
    }

//#endif
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            themeService.ThemeChanged -= OnThemeChanged;
//#if (IncludeNotes)
            syncService.StatusChanged -= OnSyncStatusChanged;
            connectivityService.StatusChanged -= OnConnectivityChanged;
//#endif
        }

        base.Dispose(disposing);
    }

//#if (IncludeNotes)
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
            ? $"{bytes} B"
            : string.Create(CultureInfo.InvariantCulture, $"{size:0.#} {units[unit]}");
    }

    private async Task LoadStorageKindAsync()
    {
        try
        {
            switchableProfiles = syncService.Profiles.Where(storageResolver.IsSwitchable).ToList();
            if (switchableProfiles.Count > 0)
            {
                StorageKind = (await storageResolver.GetStateAsync(switchableProfiles[0])).Kind;
                IsOpfsSupported = await storageResolver.IsSupportedAsync(SyncStorageKind.Opfs);
            }
        }
        catch (Exception)
        {
            switchableProfiles = [];
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
        OnPropertyChanged(nameof(ConnectivityText));
        OnPropertyChanged(nameof(IsForcedOffline));
        OnPropertyChanged(nameof(CanSyncNow));
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus e)
    {
        RefreshSync();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus e)
    {
        RefreshSync();
    }

//#endif
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTheme));
    }
//#if (IncludeNotes)

    /// <summary>
    /// Shows move progress as it is reported, on the caller's context (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class ProgressReporter(SettingsViewModel owner)
        : IProgress<SyncStorageMigrationProgress>
    {
        public void Report(SyncStorageMigrationProgress value)
        {
            owner.SetMigrationProgress(value);
        }
    }
//#endif
}
