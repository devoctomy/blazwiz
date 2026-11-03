using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the sync status pill floating over the app, and its popover: turn sync on or off, sync now.
/// </summary>
public partial class SyncPillViewModel : ViewModelBase
{
    private readonly ISyncService syncService;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncScheduler syncScheduler;
    private readonly ISyncStatusPresenter syncStatusPresenter;

    private bool isPopoverOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPillViewModel"/> class.
    /// </summary>
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="syncStatusPresenter">Describes the sync status.</param>
    public SyncPillViewModel(
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.syncStatusPresenter = syncStatusPresenter ?? throw new ArgumentNullException(nameof(syncStatusPresenter));

        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Gets how the pill looks: icon, tone, text and tooltip.
    /// </summary>
    public SyncStatusPresentation Pill => syncStatusPresenter.Present(syncService.Status, connectivityService.Status);

    /// <summary>
    /// Gets the sync status.
    /// </summary>
    public SyncStatus Status => syncService.Status;

    /// <summary>
    /// Gets a value indicating whether the popover is open.
    /// </summary>
    public bool IsPopoverOpen
    {
        get => isPopoverOpen;
        private set => SetProperty(ref isPopoverOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether sync is turned on.
    /// </summary>
    public bool IsSyncEnabled => connectivityService.IsSyncEnabled;

    /// <summary>
    /// Gets a value indicating whether a sync can be started now: online and not already syncing.
    /// </summary>
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing;

    /// <summary>
    /// Opens or closes the popover.
    /// </summary>
    [RelayCommand]
    public void TogglePopover()
    {
        IsPopoverOpen = !IsPopoverOpen;
    }

    /// <summary>
    /// Closes the popover.
    /// </summary>
    [RelayCommand]
    public void ClosePopover()
    {
        IsPopoverOpen = false;
    }

    /// <summary>
    /// Turns sync on or off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleSyncAsync()
    {
        await connectivityService.SetSyncEnabledAsync(!connectivityService.IsSyncEnabled);
        Refresh();
    }

    /// <summary>
    /// Syncs every profile now, if it can.
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

    /// <inheritdoc/>
    public override Task OnInitializedAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            syncService.StatusChanged -= OnSyncStatusChanged;
            connectivityService.StatusChanged -= OnConnectivityChanged;
        }

        base.Dispose(disposing);
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Pill));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsSyncEnabled));
        OnPropertyChanged(nameof(CanSyncNow));
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus e)
    {
        Refresh();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus e)
    {
        Refresh();
    }
}
