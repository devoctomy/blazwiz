using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class SyncSessionHandlerTests
{
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<ISyncStorageMigrationService> mockStorageMigration = new();
    private readonly Mock<ISyncStorageChannel> mockStorageChannel = new();
    private readonly Mock<ISyncEncryptionService> mockEncryption = new();
    private readonly Mock<ISyncProtectedProfileService> mockProtected = new();
    private readonly Mock<ISyncFolderAccessMonitor> mockFolderAccess = new();
    private readonly Mock<IPersistentStorageService> mockPersistentStorage = new();
    private readonly UserSession session = new() { Username = "bob@example.com" };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(null!, mockScheduler.Object, mockStorageMigration.Object, mockStorageChannel.Object, mockEncryption.Object, mockProtected.Object, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, null!, mockStorageMigration.Object, mockStorageChannel.Object, mockEncryption.Object, mockProtected.Object, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("storageMigrationService", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, null!, mockStorageChannel.Object, mockEncryption.Object, mockProtected.Object, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("storageChannel", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, mockStorageMigration.Object, null!, mockEncryption.Object, mockProtected.Object, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("encryptionService", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, mockStorageMigration.Object, mockStorageChannel.Object, null!, mockProtected.Object, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("protectedProfiles", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, mockStorageMigration.Object, mockStorageChannel.Object, mockEncryption.Object, null!, mockFolderAccess.Object, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("folderAccessMonitor", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, mockStorageMigration.Object, mockStorageChannel.Object, mockEncryption.Object, mockProtected.Object, null!, mockPersistentStorage.Object)).ParamName);
        Assert.Equal("persistentStorageService", Assert.Throws<ArgumentNullException>(() => new SyncSessionHandler(mockConnectivity.Object, mockScheduler.Object, mockStorageMigration.Object, mockStorageChannel.Object, mockEncryption.Object, mockProtected.Object, mockFolderAccess.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenShellLoading_WhenOnShellLoading_ThenConnectivityInitialized()
    {
        // Act
        await CreateSut().OnShellLoadingAsync(session);

        // Assert
        mockConnectivity.Verify(x => x.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenSignedIn_WhenOnSignedIn_ThenPersistentStorageRequested()
    {
        // Act
        await CreateSut().OnSignedInAsync();

        // Assert
        mockPersistentStorage.Verify(x => x.RequestAfterSignInAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenSessionStarts_WhenOnSessionStarted_ThenListensToOtherTabsRecoversStorageAndUnlocksBeforeSyncStarts()
    {
        // Arrange
        var calls = new List<string>();
        mockStorageChannel.Setup(x => x.StartAsync()).Callback(() => calls.Add("channel")).Returns(Task.CompletedTask);
        mockStorageMigration.Setup(x => x.RecoverAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("recover")).Returns(Task.CompletedTask);
        mockEncryption.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("encryption")).ReturnsAsync(E2eStatus.Unlocked);
        mockProtected.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("protected profiles")).ReturnsAsync([]);
        mockFolderAccess.Setup(x => x.Start()).Callback(() => calls.Add("watch folder"));
        mockFolderAccess.Setup(x => x.CheckAsync()).Callback(() => calls.Add("check folder")).ReturnsAsync(false);
        mockScheduler.Setup(x => x.StartAsync()).Callback(() => calls.Add("sync")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().OnSessionStartedAsync(session);

        // Assert
        Assert.Equal(["channel", "recover", "encryption", "protected profiles", "watch folder", "check folder", "sync"], calls);
    }

    [Fact]
    public async Task GivenRecoveryFails_WhenOnSessionStarted_ThenSyncStillStarts()
    {
        // Arrange
        mockStorageMigration.Setup(x => x.RecoverAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        await CreateSut().OnSessionStartedAsync(session);

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenEncryptionFailsToInitialize_WhenOnSessionStarted_ThenSyncStillStarts()
    {
        // Arrange
        mockEncryption.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidDataException("bad keys"));

        // Act
        await CreateSut().OnSessionStartedAsync(session);

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenFolderNeedsPermission_WhenOnSessionStarted_ThenSyncWaits()
    {
        // Arrange
        mockFolderAccess.Setup(x => x.CheckAsync()).ReturnsAsync(true);

        // Act
        await CreateSut().OnSessionStartedAsync(session);

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSyncFailsToStart_WhenOnSessionStarted_ThenNoException()
    {
        // Arrange
        mockScheduler.Setup(x => x.StartAsync()).ThrowsAsync(new InvalidOperationException());

        // Act
        var exception = await Record.ExceptionAsync(() => CreateSut().OnSessionStartedAsync(session));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenSigningOut_WhenOnSigningOut_ThenStopsWatchingTheFolderAndStopsSync()
    {
        // Act
        await CreateSut().OnSigningOutAsync();

        // Assert
        mockFolderAccess.Verify(x => x.Stop(), Times.Once);
        mockScheduler.Verify(x => x.StopAsync(), Times.Once);
        mockProtected.Verify(x => x.Unload(), Times.Once);
    }

    [Fact]
    public async Task GivenProtectedProfilesFailToLoad_WhenOnSessionStarted_ThenSyncStillStarts()
    {
        // Arrange
        mockProtected.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not Cognito"));
        mockFolderAccess.Setup(x => x.CheckAsync()).ReturnsAsync(false);

        // Act
        await CreateSut().OnSessionStartedAsync(session);

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
    }

    private SyncSessionHandler CreateSut() => new(
        mockConnectivity.Object,
        mockScheduler.Object,
        mockStorageMigration.Object,
        mockStorageChannel.Object,
        mockEncryption.Object,
        mockProtected.Object,
        mockFolderAccess.Object,
        mockPersistentStorage.Object);
}
