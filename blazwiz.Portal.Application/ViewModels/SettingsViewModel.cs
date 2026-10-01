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

    private StorageDurabilityStatus? storage;
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
    ///#endif
    /// <param name="themeService">The theme service.</param>
    public SettingsViewModel(
//#if (IncludeNotes)
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter,
        IStorageDurabilityService storageDurabilityService,
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
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing;

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
}
