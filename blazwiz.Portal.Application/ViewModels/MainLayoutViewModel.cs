using Blazing.Mvvm.ComponentModel;
//#if (IncludeNotes)
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
//#endif
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Models.Settings;
//#if (IncludeNotes)
using blazwiz.Portal.Models.Sync;
//#endif
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the main application shell: sidebar, theme, current user and logout.
/// </summary>
public partial class MainLayoutViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IThemeService themeService;
    private readonly IUserSettingsService userSettingsService;
    private readonly INavigationMenuService navigationMenuService;
    private readonly INavigationService navigationService;
    private readonly IUserDisplayService userDisplayService;
    private readonly IAwsCredentialsProvider credentialsProvider;
//#if (IncludeNotes)
    private readonly ISyncService syncService;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncScheduler syncScheduler;
    private readonly ISyncStatusPresenter syncStatusPresenter;
    private readonly ISyncStorageMigrationService storageMigrationService;
    private readonly ISyncStorageChannel storageChannel;
    private readonly ISyncFolderService folderService;
    private readonly IBrowserNetworkStatus browserNetworkStatus;
    private readonly ISyncEncryptionService encryptionService;
//#endif
    private readonly AppSettings appSettings;

    private bool isReady;
    private bool isSidebarCollapsed;
    private bool isMobileMenuOpen;
    private UserSession? currentUser;
    private int contentVersion;
//#if (IncludeNotes)
    private bool isSyncPopoverOpen;
    private bool isStorageMigrating;
    private bool needsFolderAccess;
    private string? folderName;
    private string? folderAccessError;
//#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="MainLayoutViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="userSettingsService">The user settings service.</param>
    /// <param name="navigationMenuService">The navigation menu service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="userDisplayService">The user display service.</param>
    /// <param name="credentialsProvider">Holds the session's AWS credentials: fetched once signed in, forgotten on sign out.</param>
    ///#if (IncludeNotes)
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="syncStatusPresenter">Describes the sync status.</param>
    /// <param name="storageMigrationService">Finishes or undoes a storage move cut short.</param>
    /// <param name="storageChannel">Hears about storage moves in other tabs.</param>
    /// <param name="folderService">Checks and restores access to the folder the files are kept in.</param>
    /// <param name="browserNetworkStatus">Says when the app comes back to the foreground.</param>
    /// <param name="encryptionService">Unlocks end to end encryption before sync starts.</param>
    ///#endif
    /// <param name="appSettings">The application settings.</param>
    public MainLayoutViewModel(
        IAuthenticationService authService,
        IThemeService themeService,
        IUserSettingsService userSettingsService,
        INavigationMenuService navigationMenuService,
        INavigationService navigationService,
        IUserDisplayService userDisplayService,
        IAwsCredentialsProvider credentialsProvider,
//#if (IncludeNotes)
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        ISyncStatusPresenter syncStatusPresenter,
        ISyncStorageMigrationService storageMigrationService,
        ISyncStorageChannel storageChannel,
        ISyncFolderService folderService,
        IBrowserNetworkStatus browserNetworkStatus,
        ISyncEncryptionService encryptionService,
//#endif
        AppSettings appSettings)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        this.userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        this.navigationMenuService = navigationMenuService ?? throw new ArgumentNullException(nameof(navigationMenuService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
        this.credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
//#if (IncludeNotes)
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.syncStatusPresenter = syncStatusPresenter ?? throw new ArgumentNullException(nameof(syncStatusPresenter));
        this.storageMigrationService = storageMigrationService ?? throw new ArgumentNullException(nameof(storageMigrationService));
        this.storageChannel = storageChannel ?? throw new ArgumentNullException(nameof(storageChannel));
        this.folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        this.browserNetworkStatus = browserNetworkStatus ?? throw new ArgumentNullException(nameof(browserNetworkStatus));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
//#endif
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

        this.themeService.ThemeChanged += OnThemeChanged;
//#if (IncludeNotes)
        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
        this.storageChannel.MessageReceived += OnStorageChannelMessage;
        this.browserNetworkStatus.BecameVisible += OnBecameVisible;
//#endif
    }

    /// <summary>
    /// Gets a value indicating whether the user has been verified and the shell can be shown.
    /// </summary>
    public bool IsReady
    {
        get => isReady;
        private set => SetProperty(ref isReady, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sidebar is collapsed to icons only (desktop).
    /// </summary>
    public bool IsSidebarCollapsed
    {
        get => isSidebarCollapsed;
        private set => SetProperty(ref isSidebarCollapsed, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sidebar is open as an overlay (mobile).
    /// </summary>
    public bool IsMobileMenuOpen
    {
        get => isMobileMenuOpen;
        private set => SetProperty(ref isMobileMenuOpen, value);
    }

    /// <summary>
    /// Gets the signed in user.
    /// </summary>
    public UserSession? CurrentUser
    {
        get => currentUser;
        private set
        {
            if (SetProperty(ref currentUser, value))
            {
                OnPropertyChanged(nameof(UserInitials));
            }
        }
    }

    /// <summary>
    /// Gets the initials of the signed in user, for the avatar.
    /// </summary>
    public string UserInitials => userDisplayService.GetInitials(CurrentUser?.DisplayName);

    /// <summary>
    /// Gets the sidebar navigation items.
    /// </summary>
    public IReadOnlyList<NavigationItem> NavigationItems => navigationMenuService.GetMainItems();

    /// <summary>
    /// Gets the Bootstrap icon for the theme toggle button.
    /// </summary>
    public string ThemeIcon => themeService.IsDark ? "bi-sun" : "bi-moon-stars";

    /// <summary>
    /// Gets the application settings.
    /// </summary>
    public AppSettings AppSettings => appSettings;

    /// <summary>
    /// Gets a number that changes when the page content must be shown again from scratch (it is used as the
    /// content's key), e.g. after a storage folder is reconnected, so pages reload their files.
    /// </summary>
    public int ContentVersion
    {
        get => contentVersion;
        private set => SetProperty(ref contentVersion, value);
    }

//#if (IncludeNotes)
    /// <summary>
    /// Gets what the sync status pill shows.
    /// </summary>
    public SyncStatusPresentation SyncPill => syncStatusPresenter.Present(syncService.Status, connectivityService.Status);

    /// <summary>
    /// Gets the sync status.
    /// </summary>
    public SyncStatus SyncStatus => syncService.Status;

    /// <summary>
    /// Gets a value indicating whether the sync popover is open.
    /// </summary>
    public bool IsSyncPopoverOpen
    {
        get => isSyncPopoverOpen;
        private set => SetProperty(ref isSyncPopoverOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether another tab is moving the files to another storage, so this one must wait.
    /// </summary>
    public bool IsStorageMigrating
    {
        get => isStorageMigrating;
        private set => SetProperty(ref isStorageMigrating, value);
    }

    /// <summary>
    /// Gets a value indicating whether the files are kept in a folder the browser needs permission to again, so sync
    /// waits until the user reconnects it.
    /// </summary>
    public bool NeedsFolderAccess
    {
        get => needsFolderAccess;
        private set => SetProperty(ref needsFolderAccess, value);
    }

    /// <summary>
    /// Gets why reconnecting the folder did not work, or null.
    /// </summary>
    public string? FolderAccessError
    {
        get => folderAccessError;
        private set => SetProperty(ref folderAccessError, value);
    }

    /// <summary>
    /// Gets the name of the folder to reconnect, when known.
    /// </summary>
    public string? FolderName
    {
        get => folderName;
        private set => SetProperty(ref folderName, value);
    }

    /// <summary>
    /// Gets a value indicating whether offline mode is on.
    /// </summary>
    public bool IsForcedOffline => connectivityService.IsForcedOffline;

    /// <summary>
    /// Gets a value indicating whether Sync now can be used (online and not already syncing).
    /// </summary>
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing;

//#endif
    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await themeService.InitializeAsync();

        CurrentUser = await authService.GetCurrentUserAsync();
        if (CurrentUser == null)
        {
            navigationService.NavigateTo("/login");
            return;
        }

        var settings = await userSettingsService.GetSettingsAsync();
        IsSidebarCollapsed = settings.SidebarCollapsed;
//#if (IncludeNotes)
        await connectivityService.InitializeAsync();
        RefreshSync();
//#endif
        IsReady = true;

        // Signed in (or opened with a saved session): get the session's AWS credentials in the background.
        _ = LoadAwsCredentialsAsync();
//#if (IncludeNotes)

        // Signed in (or opened with a saved session): sync in the background while the page loads.
        _ = StartSyncAsync();
//#endif
    }

    /// <summary>
    /// Collapses or expands the sidebar and remembers the choice.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleSidebarAsync()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;

        var settings = await userSettingsService.GetSettingsAsync();
        settings.SidebarCollapsed = IsSidebarCollapsed;
        await userSettingsService.SaveSettingsAsync(settings);
    }

    /// <summary>
    /// Opens or closes the sidebar overlay on small screens.
    /// </summary>
    [RelayCommand]
    public void ToggleMobileMenu()
    {
        IsMobileMenuOpen = !IsMobileMenuOpen;
    }

    /// <summary>
    /// Closes the sidebar overlay on small screens.
    /// </summary>
    [RelayCommand]
    public void CloseMobileMenu()
    {
        IsMobileMenuOpen = false;
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ToggleThemeAsync()
    {
        return themeService.ToggleThemeAsync();
    }

//#if (IncludeNotes)
    /// <summary>
    /// Opens or closes the sync popover.
    /// </summary>
    [RelayCommand]
    public void ToggleSyncPopover()
    {
        IsSyncPopoverOpen = !IsSyncPopoverOpen;
    }

    /// <summary>
    /// Closes the sync popover.
    /// </summary>
    [RelayCommand]
    public void CloseSyncPopover()
    {
        IsSyncPopoverOpen = false;
    }

    /// <summary>
    /// Turns offline mode on or off. It is remembered until switched off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleOfflineModeAsync()
    {
        await connectivityService.SetForcedOfflineAsync(!connectivityService.IsForcedOffline);
        RefreshSync();
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
    /// Asks the browser for permission to the folder the files are kept in again. Once it is allowed, the page content
    /// is shown again (so it reloads its files) and sync starts. The page is not reloaded: some browsers (e.g. Chrome
    /// on Android) only keep the permission for the current page.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ReconnectFolderAsync()
    {
        try
        {
            if (!await folderService.ReconnectAsync())
            {
                FolderAccessError = "Access to the folder was not allowed.";
                return;
            }

            if ((await folderService.GetStatusAsync())?.IsGranted != true)
            {
                FolderAccessError = "The browser did not keep access to the folder. Try again, or move your notes to browser storage in Settings.";
                return;
            }
        }
        catch (Exception ex)
        {
            FolderAccessError = $"The folder could not be reconnected: {ex.Message}";
            return;
        }

        FolderAccessError = null;
        NeedsFolderAccess = false;
        ContentVersion++;
        await StartSchedulerAsync();
    }

//#endif
    /// <summary>
    /// Signs the user out and returns to the login page.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LogoutAsync()
    {
//#if (IncludeNotes)
        // Stop syncing first. Local data stays, partitioned by user.
        await syncScheduler.StopAsync();
//#endif
        await authService.LogoutAsync();
        credentialsProvider.Clear();
        navigationService.NavigateTo("/login");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            themeService.ThemeChanged -= OnThemeChanged;
//#if (IncludeNotes)
            syncService.StatusChanged -= OnSyncStatusChanged;
            connectivityService.StatusChanged -= OnConnectivityChanged;
            storageChannel.MessageReceived -= OnStorageChannelMessage;
            browserNetworkStatus.BecameVisible -= OnBecameVisible;
//#endif
        }

        base.Dispose(disposing);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ThemeIcon));
    }

    private async Task LoadAwsCredentialsAsync()
    {
        try
        {
            await credentialsProvider.GetCredentialsAsync();
        }
        catch (Exception ex) when (ex is CognitoException or HttpRequestException)
        {
            // Offline or refused: whatever needs AWS asks again when it is used.
        }
    }
//#if (IncludeNotes)

    private async Task StartSyncAsync()
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

        if (!await CheckFolderAccessAsync())
        {
            await StartSchedulerAsync();
        }
    }

    private async Task StartSchedulerAsync()
    {
        try
        {
            await syncScheduler.StartAsync();
        }
        catch (Exception)
        {
            // Sync failures show in the status pill and the sync log; the app keeps working locally.
        }
    }

    // Shows the reconnect banner if the files are kept in a folder the browser no longer allows access to. The browser
    // only asks again after a click, so the user must press Reconnect. Returns true if access is needed.
    private async Task<bool> CheckFolderAccessAsync()
    {
        try
        {
            if (!await folderService.NeedsReconnectAsync())
            {
                return false;
            }

            FolderName = (await folderService.GetStatusAsync())?.Name;
            NeedsFolderAccess = true;
            return true;
        }
        catch (Exception)
        {
            // Sync reports the problem itself if the folder cannot be used.
            return false;
        }
    }

    // Phones (e.g. Chrome on Android) can take the permission away while the app is in the background.
    private async void OnBecameVisible(object? sender, EventArgs e)
    {
        try
        {
            if (!NeedsFolderAccess && await CheckFolderAccessAsync())
            {
                await syncScheduler.StopAsync();
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    private void RefreshSync()
    {
        OnPropertyChanged(nameof(SyncPill));
        OnPropertyChanged(nameof(SyncStatus));
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

    private void OnStorageChannelMessage(object? sender, SyncStorageChannelMessage e)
    {
        if (e == SyncStorageChannelMessage.Migrating)
        {
            IsStorageMigrating = true;
            return;
        }

        // Another tab moved the files: start again against the storage now in use.
        navigationService.Reload();
    }
//#endif
}
