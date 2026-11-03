using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.Storage;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.ViewModels;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class SyncSettingsViewModelTests
{
    private readonly TestText testText = new();

    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<IStorageDurabilityService> mockStorage = new();
    private readonly Mock<ISyncStorageResolver> mockResolver = new();
    private readonly Mock<ISyncStorageMigrationService> mockMigration = new();
    private readonly Mock<ISyncFolderService> mockFolder = new();
    private SyncStatus syncStatus = new() { PendingCount = 3, LastErrorCode = "Sync.FilesFailed", LastErrorArguments = [1], State = SyncState.Error };
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.Unreachable, LastCheckedAt = DateTimeOffset.UnixEpoch };

    public SyncSettingsViewModelTests()
    {
        mockSync.SetupGet(x => x.Status).Returns(() => syncStatus);
        mockConnectivity.SetupGet(x => x.Status).Returns(() => connectivity);
    }

    [Fact]
    public void GivenDisposed_WhenStatusChangesRaised_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, syncStatus);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Assert
        Assert.Empty(changed);
    }

    [Fact]
    public void GivenNullSyncDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var presenter = new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors);
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(null!, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, null!, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, null!, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncStatusPresenter", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageDurabilityService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, null!, mockResolver.Object, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageResolver", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, null!, mockMigration.Object, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("folderService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, null!, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageMigrationService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, null!, mockFolder.Object, testText.Sync, testText.Errors)).ParamName);
    }

    [Fact]
    public async Task GivenSwitchableProfile_WhenInitialized_ThenShowsStorageInUseAndOpfsSupport()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.Opfs, opfsSupported: true);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.CanSwitchStorage);
        Assert.Equal(SyncStorageKind.Opfs, sut.StorageKind);
        Assert.True(sut.IsOpfsSupported);
    }

    [Fact]
    public async Task GivenNoSwitchableProfile_WhenInitialized_ThenStorageChoiceHidden()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns(["notes"]);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.CanSwitchStorage);
        mockResolver.Verify(x => x.GetStateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenStorageCannotBeRead_WhenInitialized_ThenStorageChoiceHidden()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        mockResolver.Setup(x => x.GetStateAsync("notes")).ThrowsAsync(new InvalidOperationException("Sign in to use your files."));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.CanSwitchStorage);
    }

    [Theory]
    [InlineData(SyncStorageKind.IndexedDb, "IndexedDB")]
    [InlineData(SyncStorageKind.Opfs, "OPFS")]
    public void GivenStorage_WhenGetStorageName_ThenReturnsName(SyncStorageKind kind, string expected)
    {
        Assert.Equal(expected, CreateSut().GetStorageName(kind));
    }

    [Fact]
    public async Task GivenOtherStorage_WhenRequestStorageChange_ThenCountsFilesAndAsks()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true, fileCount: 2);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.True(sut.IsStorageDialogOpen);
        Assert.Equal(SyncStorageKind.Opfs, sut.PendingStorageKind);
        Assert.Equal(2, sut.PendingFileCount);
    }

    [Fact]
    public async Task GivenStorageInUse_WhenRequestStorageChange_ThenNothingAsked()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.IndexedDb);

        // Assert
        Assert.False(sut.IsStorageDialogOpen);
    }

    [Fact]
    public async Task GivenFilesCannotBeListed_WhenRequestStorageChange_ThenShowsErrorWithoutAsking()
    {
        // Arrange
        var files = SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        files.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.False(sut.IsStorageDialogOpen);
        Assert.Equal("Your files could not be read: Something went wrong.", sut.StorageError);
    }

    [Fact]
    public async Task GivenDialogOpen_WhenCloseStorageDialog_ThenClosesWithoutMoving()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Act
        sut.CloseStorageDialogCommand.Execute(null);

        // Assert
        Assert.False(sut.IsStorageDialogOpen);
        mockMigration.Verify(x => x.MigrateAsync(It.IsAny<SyncStorageKind>(), It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenConfirmed_WhenConfirmStorageChange_ThenMovesShowingProgressAndSelectsTarget()
    {
        // Arrange
        connectivity = new() { IsOnline = true };
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);
        string? syncingText = null;
        string? progressText = null;
        bool? migratingDuring = null;
        bool? canSyncDuring = null;
        bool? closeIgnored = null;
        mockMigration.Setup(x => x.MigrateAsync(SyncStorageKind.Opfs, It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<SyncStorageKind, IProgress<SyncStorageMigrationProgress>?, CancellationToken>((_, progress, _) =>
            {
                progress!.Report(new SyncStorageMigrationProgress("notes", 0, 0, isSyncing: true));
                syncingText = sut.MigrationText;
                progress.Report(new SyncStorageMigrationProgress("notes", 3, 10));
                progressText = sut.MigrationText;
                migratingDuring = sut.IsMigrating;
                canSyncDuring = sut.CanSyncNow;
                sut.CloseStorageDialogCommand.Execute(null);
                closeIgnored = sut.IsStorageDialogOpen;
            })
            .Returns(Task.CompletedTask);

        // Act
        await sut.ConfirmStorageChangeCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Syncing before moving…", syncingText);
        Assert.Equal("Moving files: 3 of 10", progressText);
        Assert.True(migratingDuring);
        Assert.False(canSyncDuring);
        Assert.True(closeIgnored);
        Assert.Equal(SyncStorageKind.Opfs, sut.StorageKind);
        Assert.False(sut.IsMigrating);
        Assert.False(sut.IsStorageDialogOpen);
        Assert.Null(sut.MigrationText);
        Assert.Null(sut.StorageError);
    }

    [Fact]
    public async Task GivenMoveFails_WhenConfirmStorageChange_ThenShowsErrorAndKeepsStorage()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);
        mockMigration.Setup(x => x.MigrateAsync(SyncStorageKind.Opfs, It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncStorageMigrationException(SyncErrorCodes.StorageCopyMismatch, "notes/a.md"));

        // Act
        await sut.ConfirmStorageChangeCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(SyncStorageKind.IndexedDb, sut.StorageKind);
        Assert.Equal("Your files could not be moved: 'notes/a.md' did not copy correctly.", sut.StorageError);
        Assert.False(sut.IsMigrating);
        Assert.False(sut.IsStorageDialogOpen);
    }

    [Fact]
    public async Task GivenFolderInUse_WhenInitialized_ThenShowsFolderNameAndSupport()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.LocalFolder, opfsSupported: false);
        mockResolver.Setup(x => x.IsSupportedAsync(SyncStorageKind.LocalFolder)).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "granted" });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsFolderSupported);
        Assert.Equal("Notes", sut.FolderName);
        Assert.Equal("File System", sut.GetStorageName(SyncStorageKind.LocalFolder));
    }

    [Fact]
    public async Task GivenPickerCancelled_WhenRequestFolder_ThenNothingAsked()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.LocalFolder);

        // Assert
        mockFolder.Verify(x => x.PickAsync(), Times.Once);
        Assert.False(sut.IsStorageDialogOpen);
        Assert.Null(sut.StorageError);
    }

    [Fact]
    public async Task GivenPickerFails_WhenRequestFolder_ThenShowsError()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        mockFolder.Setup(x => x.PickAsync()).ThrowsAsync(new InvalidOperationException("SecurityError"));
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.LocalFolder);

        // Assert
        Assert.False(sut.IsStorageDialogOpen);
        Assert.Equal("The folder could not be opened: Something went wrong.", sut.StorageError);
    }

    [Fact]
    public async Task GivenFolderPicked_WhenRequestAndConfirm_ThenAsksNamingTheFolderAndShowsItOnceMoved()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true, fileCount: 1);
        mockFolder.Setup(x => x.PickAsync()).ReturnsAsync("Notes");
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.LocalFolder);

        // Assert
        Assert.True(sut.IsStorageDialogOpen);
        Assert.Equal("File System (Notes)", sut.PendingStorageName);

        // Act
        await sut.ConfirmStorageChangeCommand.ExecuteAsync(null);

        // Assert
        mockMigration.Verify(x => x.MigrateAsync(SyncStorageKind.LocalFolder, It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(SyncStorageKind.LocalFolder, sut.StorageKind);
        Assert.Equal("Notes", sut.FolderName);
        Assert.Null(sut.PendingStorageName);
    }

    [Fact]
    public async Task GivenOtherStorageAsked_WhenPendingStorageName_ThenJustItsName()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.Equal("OPFS", sut.PendingStorageName);
        mockFolder.Verify(x => x.PickAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenNothingAsked_WhenConfirmStorageChange_ThenNothingMoved()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ConfirmStorageChangeCommand.ExecuteAsync(null);

        // Assert
        mockMigration.Verify(x => x.MigrateAsync(It.IsAny<SyncStorageKind>(), It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        Assert.Equal("1 file could not be synced.", sut.LastErrorText);
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
    public async Task GivenSyncOff_WhenToggled_ThenTurnedOn()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(false);
        var sut = CreateSut();

        // Act
        await sut.ToggleSyncCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsSyncEnabled);
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(true), Times.Once);
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
        Assert.Equal(2, changed.Count(c => c == nameof(SyncSettingsViewModel.SyncDisplay)));
    }

    private Mock<ISyncFileStore> SetupSwitchableNotes(SyncStorageKind kind, bool opfsSupported, int fileCount = 0)
    {
        var files = new Mock<ISyncFileStore>();
        files.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, fileCount).Select(i => new SyncFileInfo { Key = $"notes/{i}.md" }).ToList());
        mockSync.SetupGet(x => x.Profiles).Returns(["notes"]);
        mockResolver.Setup(x => x.IsSwitchable("notes")).Returns(true);
        mockResolver.Setup(x => x.GetStateAsync("notes")).ReturnsAsync(() => new SyncStorageState { Profile = "notes", Owner = "bob", Kind = kind });
        mockResolver.Setup(x => x.IsSupportedAsync(SyncStorageKind.Opfs)).ReturnsAsync(opfsSupported);
        mockResolver.Setup(x => x.GetStore("notes", kind)).Returns(files.Object);
        return files;
    }

    private SyncSettingsViewModel CreateSut() => new(
        mockSync.Object,
        mockConnectivity.Object,
        mockScheduler.Object,
        new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors),
        mockStorage.Object,
        mockResolver.Object,
        mockMigration.Object,
        mockFolder.Object,
        testText.Sync, testText.Errors);
}
