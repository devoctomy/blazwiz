//#if (IncludeNotes)
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.Storage;
using blazwiz.Portal.Application.Services;
//#endif
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<IThemeService> mockTheme = new();
//#if (IncludeNotes)
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<IStorageDurabilityService> mockStorage = new();
    private SyncStatus syncStatus = new() { PendingCount = 3, LastError = "1 file could not be synced.", State = SyncState.Error };
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.Unreachable, LastCheckedAt = DateTimeOffset.UnixEpoch };

    public SettingsViewModelTests()
    {
        mockSync.SetupGet(x => x.Status).Returns(() => syncStatus);
        mockConnectivity.SetupGet(x => x.Status).Returns(() => connectivity);
    }
//#endif

    [Fact]
    public void GivenNullThemeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateSut(withoutTheme: true));
        Assert.Equal("themeService", exception.ParamName);
    }

    [Fact]
    public void GivenThemeService_WhenGettingCurrentTheme_ThenReturnsServiceTheme()
    {
        // Arrange
        mockTheme.SetupGet(x => x.CurrentTheme).Returns(ThemeNames.Dark);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
    }

    [Fact]
    public async Task GivenTheme_WhenSelectTheme_ThenSetsTheme()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SelectThemeCommand.ExecuteAsync(ThemeNames.Dark);

        // Assert
        mockTheme.Verify(x => x.SetThemeAsync(ThemeNames.Dark), Times.Once);
    }

    [Fact]
    public void GivenThemeChanges_WhenThemeChangedRaised_ThenCurrentThemeChangeIsNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(SettingsViewModel.CurrentTheme), changed);
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
//#if (IncludeNotes)
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
//#endif

        // Assert
        Assert.Empty(changed);
    }

//#if (IncludeNotes)
    [Fact]
    public void GivenNullSyncDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var presenter = new SyncStatusPresenter();
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockTheme.Object)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(mockSync.Object, null!, mockScheduler.Object, presenter, mockStorage.Object, mockTheme.Object)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(mockSync.Object, mockConnectivity.Object, null!, presenter, mockStorage.Object, mockTheme.Object)).ParamName);
        Assert.Equal("syncStatusPresenter", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, mockStorage.Object, mockTheme.Object)).ParamName);
        Assert.Equal("storageDurabilityService", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, null!, mockTheme.Object)).ParamName);
    }

    [Fact]
    public async Task GivenSyncState_WhenInitialized_ThenSyncCardShowsStateConnectivityPendingAndError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockConnectivity.Verify(x => x.InitializeAsync(), Times.Once);
        Assert.Equal("Offline · 3 pending", sut.SyncDisplay.Text);
        Assert.Equal("Offline: S3 unreachable", sut.ConnectivityText);
        Assert.Equal(3, sut.SyncStatus.PendingCount);
        Assert.Equal("1 file could not be synced.", sut.SyncStatus.LastError);
        Assert.False(sut.CanSyncNow);
    }

    [Fact]
    public async Task GivenPersistentStorage_WhenInitialized_ThenStorageLineShowsPersistentAndUsage()
    {
        // Arrange
        mockStorage.Setup(x => x.GetStatusAsync()).ReturnsAsync(new StorageDurabilityStatus { IsSupported = true, IsPersisted = true, UsageBytes = 2_202_010 });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsStoragePersisted);
        Assert.Equal("Persistent · 2.1 MB used", sut.StorageText);
        Assert.False(sut.ShowInstallHint);
    }

    [Fact]
    public async Task GivenBestEffortStorageNotInstalled_WhenInitialized_ThenInstallHintShown()
    {
        // Arrange
        mockStorage.Setup(x => x.GetStatusAsync()).ReturnsAsync(new StorageDurabilityStatus { IsSupported = true, UsageBytes = 512 });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsStoragePersisted);
        Assert.Equal("Best effort · 512 B used", sut.StorageText);
        Assert.True(sut.ShowInstallHint);
    }

    [Fact]
    public async Task GivenBestEffortStorageAlreadyInstalled_WhenInitialized_ThenNoInstallHint()
    {
        // Arrange
        mockStorage.Setup(x => x.GetStatusAsync()).ReturnsAsync(new StorageDurabilityStatus { IsSupported = true, IsInstalled = true });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Best effort", sut.StorageText);
        Assert.False(sut.ShowInstallHint);
    }

    [Fact]
    public async Task GivenStorageUnavailable_WhenInitialized_ThenStorageUnknown()
    {
        // Arrange
        mockStorage.Setup(x => x.GetStatusAsync()).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsStorageKnown);
        Assert.Equal("Unknown", sut.StorageText);
        Assert.False(sut.ShowInstallHint);
    }

    [Fact]
    public async Task GivenOnline_WhenSyncNow_ThenSchedulerSyncs()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = true, LastCheckedAt = DateTimeOffset.UnixEpoch };
        syncStatus = new SyncStatus();
        var sut = CreateSut();

        // Act
        await sut.SyncNowCommand.ExecuteAsync(null);

        // Assert
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenOffline_WhenSyncNow_ThenNothingHappens()
    {
        await CreateSut().SyncNowCommand.ExecuteAsync(null);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineModeOn_WhenToggled_ThenTurnedOff()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsForcedOffline).Returns(true);
        var sut = CreateSut();

        // Act
        await sut.ToggleOfflineModeCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsForcedOffline);
        mockConnectivity.Verify(x => x.SetForcedOfflineAsync(false), Times.Once);
    }

    [Fact]
    public void GivenSyncStatusChanges_WhenRaised_ThenSyncCardNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Assert
        Assert.Equal(2, changed.Count(c => c == nameof(SettingsViewModel.SyncDisplay)));
    }

//#endif
    private SettingsViewModel CreateSut(bool withoutTheme = false) => new(
//#if (IncludeNotes)
        mockSync.Object,
        mockConnectivity.Object,
        mockScheduler.Object,
        new SyncStatusPresenter(),
        mockStorage.Object,
//#endif
        withoutTheme ? null! : mockTheme.Object);
}
