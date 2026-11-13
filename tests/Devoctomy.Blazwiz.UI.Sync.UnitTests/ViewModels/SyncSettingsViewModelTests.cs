using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

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
    private readonly Mock<ISyncProfileRegistry> mockRegistry = new();
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
        // Arrange
        var presenter = new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors);

        // Act & Assert
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(null!, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, null!, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, null!, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncStatusPresenter", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageDurabilityService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, null!, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageResolver", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, null!, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("folderService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, null!, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("storageMigrationService", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, null!, mockFolder.Object, mockRegistry.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, null!, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new SyncSettingsViewModel(mockSync.Object, mockConnectivity.Object, mockScheduler.Object, presenter, mockStorage.Object, mockResolver.Object, mockMigration.Object, mockFolder.Object, mockRegistry.Object, testText.Sync, null!)).ParamName);
    }

    [Fact]
    public void GivenNoSyncError_WhenShown_ThenNoErrorTextAndTheInstallHintComesFromTheText()
    {
        // Arrange
        syncStatus = new SyncStatus();

        // Act
        var sut = CreateSut();

        // Assert
        Assert.Null(sut.LastErrorText);
        Assert.Equal(testText.Sync["Sync.Storage.InstallHint"].Value, sut.InstallHint);
    }

    [Fact]
    public void GivenTheStorageNotRead_WhenShown_ThenItIsNotPersisted()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.False(sut.IsStorageKnown);
        Assert.False(sut.IsStoragePersisted);
    }

    [Fact]
    public void GivenAStorageKindItDoesNotKnow_WhenGetStorageName_ThenItsValueIsShown()
    {
        // Act & Assert
        Assert.Equal("99", CreateSut().GetStorageName((SyncStorageKind)99));
    }

    [Fact]
    public async Task GivenAProfileIsAdded_WhenAListenerThrows_ThenTheErrorDoesNotEscape()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SyncSettingsViewModel.CanSwitchStorage))
            {
                throw new InvalidOperationException("listener");
            }
        };

        // Act
        var exception = Record.Exception(() => mockRegistry.Raise(x => x.ProfileAdded += null, mockRegistry.Object, "notes"));

        // Assert
        Assert.Null(exception);
        Assert.True(sut.CanSwitchStorage);
    }

    [Fact]
    public async Task GivenOfflineThenOnline_WhenConnectivityChanges_ThenSyncNowBecomesAvailable()
    {
        // Arrange
        syncStatus = new SyncStatus();
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        var offline = sut.CanSyncNow;
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        connectivity = new ConnectivityStatus { IsOnline = true, LastCheckedAt = DateTimeOffset.UnixEpoch };

        // Act
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Assert
        Assert.False(offline);
        Assert.True(sut.CanSyncNow);
        Assert.Contains(nameof(SyncSettingsViewModel.CanSyncNow), changed);
        Assert.Equal("Online", sut.ConnectivityText);
    }

    [Fact]
    public async Task GivenSyncing_WhenSyncNow_ThenNothingHappens()
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
    public async Task GivenOnlyProfilesThatCannotSwitch_WhenAnotherStorageIsRequested_ThenNothingIsAskedOrMoved()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns(["photos"]);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.False(sut.IsStorageDialogOpen);
        mockFolder.Verify(x => x.PickAsync(), Times.Never);
        mockMigration.Verify(x => x.MigrateAsync(It.IsAny<SyncStorageKind>(), It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task GivenNoProfiles_WhenInitialized_ThenTheDevicesStorageIsOffered()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockResolver.Setup(x => x.GetDeviceDefaultKindAsync()).ReturnsAsync(SyncStorageKind.Opfs);
        mockResolver.Setup(x => x.IsSupportedAsync(SyncStorageKind.Opfs)).ReturnsAsync(true);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.CanSwitchStorage);
        Assert.Equal(SyncStorageKind.Opfs, sut.StorageKind);
        Assert.True(sut.IsOpfsSupported);
    }

    [Fact]
    public async Task GivenNoProfilesAndNoDeviceStorage_WhenInitialized_ThenIndexedDbIsShown()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.CanSwitchStorage);
        Assert.Equal(SyncStorageKind.IndexedDb, sut.StorageKind);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenAnotherStorageIsChosen_ThenItIsTheDevicesStorage_WithoutAskingToMoveFiles()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockResolver.Setup(x => x.IsSupportedAsync(SyncStorageKind.Opfs)).ReturnsAsync(true);
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.Equal(SyncStorageKind.Opfs, sut.StorageKind);
        Assert.False(sut.IsStorageDialogOpen);
        Assert.Null(sut.PendingStorageKind);
        mockMigration.Verify(x => x.MigrateAsync(SyncStorageKind.Opfs, It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenAFolderIsChosen_ThenItIsPickedAndBecomesTheDevicesStorage()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockFolder.Setup(x => x.PickAsync()).ReturnsAsync("Vaults");
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.LocalFolder);

        // Assert
        Assert.Equal(SyncStorageKind.LocalFolder, sut.StorageKind);
        Assert.Equal("Vaults", sut.FolderName);
        mockMigration.Verify(x => x.MigrateAsync(SyncStorageKind.LocalFolder, It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenChoosingAStorageFails_ThenTheErrorIsShownAndTheStorageKept()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockMigration.Setup(x => x.MigrateAsync(It.IsAny<SyncStorageKind>(), It.IsAny<IProgress<SyncStorageMigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sign in to use your files."));
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RequestStorageChangeCommand.ExecuteAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.Equal(SyncStorageKind.IndexedDb, sut.StorageKind);
        Assert.NotNull(sut.StorageError);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenAProfileIsAdded_ThenItsStorageIsShown()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        SetupSwitchableNotes(SyncStorageKind.LocalFolder, opfsSupported: true);

        // Act
        mockRegistry.Raise(x => x.ProfileAdded += null, mockRegistry.Object, "notes");
        await WaitForAsync(() => sut.StorageKind == SyncStorageKind.LocalFolder);

        // Assert
        Assert.True(sut.CanSwitchStorage);
        Assert.Equal(SyncStorageKind.LocalFolder, sut.StorageKind);
        Assert.True(sut.IsOpfsSupported);
    }

    [Fact]
    public async Task GivenOneProfile_WhenItIsRemoved_ThenTheDevicesStorageIsOffered()
    {
        // Arrange
        SetupSwitchableNotes(SyncStorageKind.IndexedDb, opfsSupported: true);
        mockResolver.Setup(x => x.GetDeviceDefaultKindAsync()).ReturnsAsync(SyncStorageKind.Opfs);
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        mockSync.SetupGet(x => x.Profiles).Returns([]);

        // Act
        mockRegistry.Raise(x => x.ProfileRemoved += null, mockRegistry.Object, "notes");
        await WaitForAsync(() => sut.StorageKind == SyncStorageKind.Opfs);

        // Assert
        Assert.True(sut.CanSwitchStorage);
        Assert.Equal(SyncStorageKind.Opfs, sut.StorageKind);
    }

    [Fact]
    public async Task GivenOnlyProfilesThatCannotSwitch_WhenInitialized_ThenNoStorageIsOffered()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns(["photos"]);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.CanSwitchStorage);
        mockResolver.Verify(x => x.GetDeviceDefaultKindAsync(), Times.Never);
    }

    [Fact]
    public void GivenDisposed_WhenAProfileIsAdded_ThenStorageNotLoaded()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.Dispose();
        mockRegistry.Raise(x => x.ProfileAdded += null, mockRegistry.Object, "notes");

        // Assert
        mockSync.VerifyGet(x => x.Profiles, Times.Never);
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
        // Act & Assert
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
        // Act
        await CreateSut().SyncNowCommand.ExecuteAsync(null);

        // Assert
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
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
        mockRegistry.Object,
        testText.Sync, testText.Errors);
}
