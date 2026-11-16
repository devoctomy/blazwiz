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
    private readonly ISyncProfileDisplayNames displayNames;

    private bool isPopoverOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPillViewModel"/> class.
    /// </summary>
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="syncStatusPresenter">Describes the sync status.</param>
    /// <param name="displayNames">The names the profiles are shown by.</param>
    public SyncPillViewModel(
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter,
        ISyncProfileDisplayNames displayNames)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.syncStatusPresenter = syncStatusPresenter ?? throw new ArgumentNullException(nameof(syncStatusPresenter));
        this.displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));

        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Gets how the pill looks: icon, tone, text and tooltip. While one pass runs, its text names the profile, e.g.
    /// "Syncing Work 12 of 201".
    /// </summary>
    public SyncStatusPresentation Pill
    {
        get
        {
            var status = syncService.Status;
            var pill = syncStatusPresenter.Present(status, connectivityService.Status);
            if (pill.IsSyncing && status.Passes.Count == 1)
            {
                var pass = status.Passes[0];
                pill.Text = syncStatusPresenter.PresentPass(pass, NameOf(pass.Profile)).Text;
            }

            return pill;
        }
    }

    /// <summary>
    /// Gets a pill for each running pass, each naming its profile with its own progress, e.g. "Syncing Work 12 of 201",
    /// while more than one runs at once. Empty otherwise: a single pass's progress is in <see cref="Pill"/>.
    /// </summary>
    public IReadOnlyList<SyncPassPresentation> Passes
    {
        get
        {
            var status = syncService.Status;
            return status.Passes.Count < 2
                ? []
                : status.Passes.Select(p => syncStatusPresenter.PresentPass(p, NameOf(p.Profile))).ToList();
        }
    }

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

    /// <summary>
    /// The name a profile is shown by, or null when it has none: the display names fall back to the profile's name,
    /// which for a profile the user created is its id, and an id is never shown.
    /// </summary>
    private string? NameOf(string profile)
    {
        var name = displayNames.GetDisplayName(profile);
        return name == profile ? null : name;
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Pill));
        OnPropertyChanged(nameof(Passes));
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
