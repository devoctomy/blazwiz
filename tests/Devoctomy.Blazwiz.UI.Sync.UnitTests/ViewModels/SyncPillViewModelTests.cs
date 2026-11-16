using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class SyncPillViewModelTests
{
    private readonly TestText testText = new();

    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<ISyncProfileDisplayNames> mockDisplayNames = new();
    private SyncStatus syncStatus = new();
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.NoCredentials, LastCheckedAt = DateTimeOffset.UnixEpoch };

    public SyncPillViewModelTests()
    {
        mockSync.SetupGet(x => x.Status).Returns(() => syncStatus);
        mockConnectivity.SetupGet(x => x.Status).Returns(() => connectivity);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors);

        // Act & Assert
        var names = mockDisplayNames.Object;
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncPillViewModel(null!, mockConnectivity.Object, mockScheduler.Object, presenter, names)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncPillViewModel(mockSync.Object, null!, mockScheduler.Object, presenter, names)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncPillViewModel(mockSync.Object, mockConnectivity.Object, null!, presenter, names)).ParamName);
        Assert.Equal("syncStatusPresenter", Assert.Throws<ArgumentNullException>(() => new SyncPillViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, names)).ParamName);
        Assert.Equal("displayNames", Assert.Throws<ArgumentNullException>(() => new SyncPillViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, null!)).ParamName);
    }

    [Fact]
    public async Task GivenOffline_WhenInitialized_ThenPillShowsWhyOffline()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Offline", sut.Pill.Text);
        Assert.Equal("Offline: no credentials", sut.Pill.Tooltip);
        Assert.False(sut.CanSyncNow);
    }

    [Fact]
    public void GivenPill_WhenToggled_ThenPopoverOpensAndCloses()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.TogglePopoverCommand.Execute(null);
        Assert.True(sut.IsPopoverOpen);
        sut.ClosePopoverCommand.Execute(null);
        Assert.False(sut.IsPopoverOpen);
    }

    [Fact]
    public async Task GivenSyncOn_WhenToggled_ThenTurnedOff()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(true);
        var sut = CreateSut();

        // Act
        await sut.ToggleSyncCommand.ExecuteAsync(null);

        // Assert
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(false), Times.Once);
    }

    [Fact]
    public async Task GivenSyncOff_WhenToggled_ThenTurnedOnAndTheSwitchIsRefreshed()
    {
        // Arrange
        var enabled = false;
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(() => enabled);
        mockConnectivity.Setup(x => x.SetSyncEnabledAsync(It.IsAny<bool>())).Callback<bool>(value => enabled = value).Returns(Task.CompletedTask);
        var sut = CreateSut();
        var before = sut.IsSyncEnabled;
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.ToggleSyncCommand.ExecuteAsync(null);

        // Assert
        Assert.False(before);
        Assert.True(sut.IsSyncEnabled);
        Assert.Contains(nameof(SyncPillViewModel.IsSyncEnabled), changed);
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(true), Times.Once);
    }

    [Fact]
    public async Task GivenOnlineButSyncing_WhenSyncNow_ThenNothingHappens()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = true, LastCheckedAt = DateTimeOffset.UnixEpoch };
        syncStatus = new SyncStatus { State = SyncState.Syncing };
        var sut = CreateSut();

        // Act
        await sut.SyncNowCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.CanSyncNow);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Never);
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
        Assert.Contains(nameof(SyncPillViewModel.Pill), changed);
        Assert.Equal("Offline · 2 pending", sut.Pill.Text);
        Assert.Same(syncStatus, sut.Status);
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
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Assert
        Assert.Equal(0, changed);
    }

    [Fact]
    public void GivenTwoProfilesSyncingAtOnce_WhenPassesRead_ThenEachHasItsOwnPillWithItsNameAndProgress()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing, Passes = [new SyncPassStatus("a", 12, 201), new SyncPassStatus("b", 150, 201)] };
        var sut = CreateSut();

        // Act
        var result = sut.Passes;

        // Assert
        Assert.Equal(["a: Syncing Vault a 12 of 201", "b: Syncing Vault b 150 of 201"], result.Select(p => $"{p.Profile}: {p.Text}"));
        Assert.Equal("Syncing", sut.Pill.Text);
    }

    [Fact]
    public void GivenOneProfileSyncing_WhenPassesRead_ThenNoneAreListed_AndThePillNamesItWithItsProgress()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing, Passes = [new SyncPassStatus("a", 12, 201)] };
        var sut = CreateSut();

        // Act
        var result = sut.Passes;

        // Assert
        Assert.Empty(result);
        Assert.Equal("Syncing Vault a 12 of 201", sut.Pill.Text);
    }

    [Fact]
    public void GivenOneProfileSyncingBeforeItHasPlanned_WhenPillRead_ThenItNamesTheProfile()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing, Passes = [new SyncPassStatus("a", 0, 0)] };
        var sut = CreateSut();

        // Act
        var result = sut.Pill;

        // Assert
        Assert.Equal("Syncing Vault a", result.Text);
        Assert.True(result.IsSyncing);
    }

    [Fact]
    public void GivenSyncingWithNoPassYet_WhenPillRead_ThenSaysSyncing()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing };
        var sut = CreateSut();

        // Act
        var result = sut.Pill;

        // Assert
        Assert.Equal("Syncing", result.Text);
    }

    [Fact]
    public void GivenOneProfileWithNoNameSyncing_WhenPillRead_ThenItShowsTheProgressWithoutTheId()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing, Passes = [new SyncPassStatus("1344726a", 3, 10)] };
        var sut = CreateSut();
        mockDisplayNames.Setup(x => x.GetDisplayName("1344726a")).Returns("1344726a");

        // Act
        var result = sut.Pill;

        // Assert
        Assert.Equal("Syncing 3 of 10", result.Text);
    }

    [Fact]
    public void GivenTwoProfilesSyncingOneWithNoName_WhenPassesRead_ThenTheUnnamedOneShowsItsProgressWithoutTheId()
    {
        // Arrange
        syncStatus = new SyncStatus { State = SyncState.Syncing, Passes = [new SyncPassStatus("a", 12, 201), new SyncPassStatus("1344726a", 3, 10)] };
        var sut = CreateSut();
        mockDisplayNames.Setup(x => x.GetDisplayName("1344726a")).Returns("1344726a");

        // Act
        var result = sut.Passes;

        // Assert
        Assert.Equal(["Syncing Vault a 12 of 201", "Syncing 3 of 10"], result.Select(p => p.Text));
    }

    [Fact]
    public void GivenSyncStatusChanges_WhenRaised_ThenPassesAreNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);

        // Assert
        Assert.Contains(nameof(SyncPillViewModel.Passes), changed);
    }

    private SyncPillViewModel CreateSut()
    {
        mockDisplayNames.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns((string profile) => "Vault " + profile);
        return new(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors), mockDisplayNames.Object);
    }
}
