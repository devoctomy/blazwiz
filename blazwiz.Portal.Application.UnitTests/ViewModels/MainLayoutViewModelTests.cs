//#if (IncludeNotes)
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;
//#endif
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class MainLayoutViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IThemeService> mockTheme = new();
    private readonly Mock<IUserSettingsService> mockUserSettings = new();
    private readonly Mock<INavigationMenuService> mockMenu = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
//#if (IncludeNotes)
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<ISyncStorageMigrationService> mockStorageMigration = new();
    private readonly Mock<ISyncStorageChannel> mockStorageChannel = new();
    private SyncStatus syncStatus = new();
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.NoCredentials, LastCheckedAt = DateTimeOffset.UnixEpoch };
//#endif
    private readonly AppSettings appSettings = new();
    private readonly UserSettings userSettings = new();
    private readonly UserSession session = new() { Username = "bob@example.com", DisplayName = "Bob Hoskins" };

    public MainLayoutViewModelTests()
    {
        mockUserSettings.Setup(x => x.GetSettingsAsync()).ReturnsAsync(userSettings);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        mockUserDisplay.Setup(x => x.GetInitials("Bob Hoskins")).Returns("BH");
        mockUserDisplay.Setup(x => x.GetInitials(null)).Returns("?");
//#if (IncludeNotes)
        mockSync.SetupGet(x => x.Status).Returns(() => syncStatus);
        mockConnectivity.SetupGet(x => x.Status).Returns(() => connectivity);
//#endif
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(null!, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("themeService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, null!, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("userSettingsService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, null!, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("navigationMenuService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, null!, mockNavigation.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, null!, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("userDisplayService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, null!,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(), mockStorageMigration.Object, mockStorageChannel.Object,
//#endif
            null!)).ParamName);
    }

    [Fact]
    public void GivenMenuItems_WhenGettingNavigationItems_ThenReturnsMenuServiceItems()
    {
        // Arrange
        var items = new List<NavigationItem> { new() { Text = "Home" } };
        mockMenu.Setup(x => x.GetMainItems()).Returns(items);
        var sut = CreateSut();

        // Act & Assert
        Assert.Same(items, sut.NavigationItems);
        Assert.Same(appSettings, sut.AppSettings);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenShellIsReadyWithUserAndSidebarState()
    {
        // Arrange
        userSettings.SidebarCollapsed = true;
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsReady);
        Assert.Same(session, sut.CurrentUser);
        Assert.Equal("BH", sut.UserInitials);
        Assert.True(sut.IsSidebarCollapsed);
        mockTheme.Verify(x => x.InitializeAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenRedirectsToLoginAndIsNotReady()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsReady);
        Assert.Null(sut.CurrentUser);
        Assert.Equal("?", sut.UserInitials);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    [Fact]
    public async Task GivenExpandedSidebar_WhenToggleSidebar_ThenCollapsesAndSavesPreference()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleSidebarCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsSidebarCollapsed);
        Assert.True(userSettings.SidebarCollapsed);
        mockUserSettings.Verify(x => x.SaveSettingsAsync(userSettings), Times.Once);
    }

    [Fact]
    public async Task GivenCollapsedSidebar_WhenToggleSidebar_ThenExpands()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ToggleSidebarAsync();

        // Act
        await sut.ToggleSidebarAsync();

        // Assert
        Assert.False(sut.IsSidebarCollapsed);
        Assert.False(userSettings.SidebarCollapsed);
    }

    [Fact]
    public void GivenClosedMobileMenu_WhenToggleMobileMenu_ThenOpensAndThenCloses()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.ToggleMobileMenuCommand.Execute(null);
        Assert.True(sut.IsMobileMenuOpen);
        sut.ToggleMobileMenuCommand.Execute(null);
        Assert.False(sut.IsMobileMenuOpen);
    }

    [Fact]
    public void GivenOpenMobileMenu_WhenCloseMobileMenu_ThenCloses()
    {
        // Arrange
        var sut = CreateSut();
        sut.ToggleMobileMenu();

        // Act
        sut.CloseMobileMenuCommand.Execute(null);

        // Assert
        Assert.False(sut.IsMobileMenuOpen);
    }

    [Fact]
    public async Task GivenShell_WhenToggleTheme_ThenDelegatesToThemeService()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleThemeCommand.ExecuteAsync(null);

        // Assert
        mockTheme.Verify(x => x.ToggleThemeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true, "bi-sun")]
    [InlineData(false, "bi-moon-stars")]
    public void GivenTheme_WhenGettingThemeIcon_ThenReturnsIconForOtherTheme(bool isDark, string expected)
    {
        // Arrange
        mockTheme.SetupGet(x => x.IsDark).Returns(isDark);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(expected, sut.ThemeIcon);
    }

    [Fact]
    public void GivenThemeChanges_WhenThemeChangedRaised_ThenThemeIconChangeIsNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(MainLayoutViewModel.ThemeIcon), changed);
    }

    [Fact]
    public void GivenDisposed_WhenThemeChangedRaised_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Empty(changed);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenLogout_ThenLogsOutAndNavigatesToLogin()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LogoutCommand.ExecuteAsync(null);

        // Assert
        mockAuth.Verify(x => x.LogoutAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

//#if (IncludeNotes)
    [Fact]
    public void GivenNullSyncDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var presenter = new SyncStatusPresenter();
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, null!, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorageMigration.Object, mockStorageChannel.Object, appSettings)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockSync.Object, null!, mockScheduler.Object, presenter, mockStorageMigration.Object, mockStorageChannel.Object, appSettings)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockSync.Object, mockConnectivity.Object, null!, presenter, mockStorageMigration.Object, mockStorageChannel.Object, appSettings)).ParamName);
        Assert.Equal("syncStatusPresenter", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, mockStorageMigration.Object, mockStorageChannel.Object, appSettings)).ParamName);
        Assert.Equal("storageMigrationService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, null!, mockStorageChannel.Object, appSettings)).ParamName);
        Assert.Equal("storageChannel", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorageMigration.Object, null!, appSettings)).ParamName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenListensToOtherTabsAndRecoversStorageBeforeSyncStarts()
    {
        // Arrange
        var calls = new List<string>();
        mockStorageChannel.Setup(x => x.StartAsync()).Callback(() => calls.Add("channel")).Returns(Task.CompletedTask);
        mockStorageMigration.Setup(x => x.RecoverAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("recover")).Returns(Task.CompletedTask);
        mockScheduler.Setup(x => x.StartAsync()).Callback(() => calls.Add("sync")).Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(["channel", "recover", "sync"], calls);
    }

    [Fact]
    public async Task GivenRecoveryFails_WhenInitialized_ThenSyncStillStarts()
    {
        // Arrange
        mockStorageMigration.Setup(x => x.RecoverAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public void GivenAnotherTabMovesFiles_WhenMigratingMessage_ThenWaitsWithoutReloading()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Migrating);

        // Assert
        Assert.True(sut.IsStorageMigrating);
        Assert.Contains(nameof(MainLayoutViewModel.IsStorageMigrating), changed);
        mockNavigation.Verify(x => x.Reload(), Times.Never);
    }

    [Fact]
    public void GivenAnotherTabMovedFiles_WhenChangedMessage_ThenReloads()
    {
        // Arrange
        CreateSut();

        // Act
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Changed);

        // Assert
        mockNavigation.Verify(x => x.Reload(), Times.Once);
    }

    [Fact]
    public void GivenDisposed_WhenStorageMessage_ThenIgnored()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.Dispose();
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Changed);

        // Assert
        mockNavigation.Verify(x => x.Reload(), Times.Never);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenSyncStartsAndPillShowsWhyOffline()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockConnectivity.Verify(x => x.InitializeAsync(), Times.Once);
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
        Assert.Equal("Offline", sut.SyncPill.Text);
        Assert.Equal("Offline: no credentials", sut.SyncPill.Tooltip);
        Assert.False(sut.CanSyncNow);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenSyncNotStarted()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        // Act
        await CreateSut().OnInitializedAsync();

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSyncFailsToStart_WhenInitialized_ThenShellStillReady()
    {
        // Arrange
        mockScheduler.Setup(x => x.StartAsync()).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsReady);
    }

    [Fact]
    public void GivenPill_WhenToggled_ThenPopoverOpensAndCloses()
    {
        var sut = CreateSut();
        sut.ToggleSyncPopoverCommand.Execute(null);
        Assert.True(sut.IsSyncPopoverOpen);
        sut.CloseSyncPopoverCommand.Execute(null);
        Assert.False(sut.IsSyncPopoverOpen);
    }

    [Fact]
    public async Task GivenOfflineModeOff_WhenToggled_ThenTurnedOn()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleOfflineModeCommand.ExecuteAsync(null);

        // Assert
        mockConnectivity.Verify(x => x.SetForcedOfflineAsync(true), Times.Once);
    }

    [Fact]
    public async Task GivenOnline_WhenSyncNow_ThenSchedulerSyncs()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = true, LastCheckedAt = DateTimeOffset.UnixEpoch };
        var sut = CreateSut();

        // Act
        await sut.SyncNowCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.CanSyncNow);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenOffline_WhenSyncNow_ThenNothingHappens()
    {
        // Act
        await CreateSut().SyncNowCommand.ExecuteAsync(null);

        // Assert
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Never);
    }

    [Fact]
    public void GivenPendingChanges_WhenStatusChanges_ThenPillUpdates()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        syncStatus = new SyncStatus { PendingCount = 2 };

        // Act
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Assert
        Assert.Contains(nameof(MainLayoutViewModel.SyncPill), changed);
        Assert.Equal("Offline · 2 pending", sut.SyncPill.Text);
        Assert.Same(syncStatus, sut.SyncStatus);
    }

    [Fact]
    public async Task GivenSignedIn_WhenLogout_ThenSyncStoppedBeforeSigningOut()
    {
        // Arrange
        var order = new List<string>();
        mockScheduler.Setup(x => x.StopAsync()).Callback(() => order.Add("stop")).Returns(Task.CompletedTask);
        mockAuth.Setup(x => x.LogoutAsync()).Callback(() => order.Add("logout")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().LogoutCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["stop", "logout"], order);
    }

    [Fact]
    public void GivenDisposed_WhenSyncStatusChanges_ThenNoUpdates()
    {
        // Arrange
        var sut = CreateSut();
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;

        // Act
        sut.Dispose();
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);

        // Assert
        Assert.Equal(0, changed);
    }

//#endif
    private MainLayoutViewModel CreateSut() => new(
        mockAuth.Object,
        mockTheme.Object,
        mockUserSettings.Object,
        mockMenu.Object,
        mockNavigation.Object,
        mockUserDisplay.Object,
//#if (IncludeNotes)
        mockSync.Object,
        mockConnectivity.Object,
        mockScheduler.Object,
        new SyncStatusPresenter(),
        mockStorageMigration.Object,
        mockStorageChannel.Object,
//#endif
        appSettings);
}
