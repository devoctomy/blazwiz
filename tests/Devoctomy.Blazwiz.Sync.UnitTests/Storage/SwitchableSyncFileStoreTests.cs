using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Storage;

public class SwitchableSyncFileStoreTests
{
    private readonly Mock<ISyncStorageResolver> mockResolver = new();
    private readonly Mock<ISyncFileStore> mockIndexedDb = new();
    private readonly Mock<ISyncFileStore> mockOpfs = new();
    private SyncStorageState state = new() { Profile = "notes", Owner = "bob", Kind = SyncStorageKind.Opfs };

    public SwitchableSyncFileStoreTests()
    {
        mockResolver.Setup(x => x.GetStateAsync("notes")).ReturnsAsync(() => state);
        mockResolver.Setup(x => x.GetStore("notes", SyncStorageKind.IndexedDb)).Returns(mockIndexedDb.Object);
        mockResolver.Setup(x => x.GetStore("notes", SyncStorageKind.Opfs)).Returns(mockOpfs.Object);
    }

    [Fact]
    public void GivenInvalidDependencies_WhenConstructing_ThenThrows()
    {
        // Act & Assert
        Assert.Equal("profile", Assert.Throws<ArgumentException>(() => new SwitchableSyncFileStore(" ", mockResolver.Object)).ParamName);
        Assert.Equal("resolver", Assert.Throws<ArgumentNullException>(() => new SwitchableSyncFileStore("notes", null!)).ParamName);
    }

    [Fact]
    public async Task GivenStorageInUse_WhenEachCall_ThenPassedToIt()
    {
        // Arrange
        var files = new List<SyncFileInfo> { new() { Key = "notes/a.md" } };
        var file = new SyncFile { Key = "notes/a.md" };
        var at = DateTimeOffset.UnixEpoch;
        mockOpfs.Setup(x => x.ListAsync(default)).ReturnsAsync(files);
        mockOpfs.Setup(x => x.ReadAsync("notes/a.md", default)).ReturnsAsync(file);
        var sut = new SwitchableSyncFileStore("notes", mockResolver.Object);

        // Act
        var listed = await sut.ListAsync();
        var read = await sut.ReadAsync("notes/a.md");
        await sut.WriteAsync("notes/a.md", [1], at);
        await sut.CreateFolderAsync("notes/Work/");
        await sut.DeleteAsync("notes/b.md");

        // Assert
        Assert.Same(files, listed);
        Assert.Same(file, read);
        mockOpfs.Verify(x => x.WriteAsync("notes/a.md", It.IsAny<byte[]>(), at, default), Times.Once);
        mockOpfs.Verify(x => x.CreateFolderAsync("notes/Work/", default), Times.Once);
        mockOpfs.Verify(x => x.DeleteAsync("notes/b.md", default), Times.Once);
        mockIndexedDb.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenStorageChanges_WhenNextCall_ThenGoesToTheNewStorage()
    {
        // Arrange
        var sut = new SwitchableSyncFileStore("notes", mockResolver.Object);
        await sut.ListAsync();

        // Act
        state = new SyncStorageState { Profile = "notes", Owner = "bob", Kind = SyncStorageKind.IndexedDb };
        await sut.ListAsync();

        // Assert
        mockOpfs.Verify(x => x.ListAsync(default), Times.Once);
        mockIndexedDb.Verify(x => x.ListAsync(default), Times.Once);
    }

    [Fact]
    public async Task GivenFilesBeingCopied_WhenChanging_ThenRefusedButReadsAllowed()
    {
        // Arrange
        state = new SyncStorageState { Kind = SyncStorageKind.IndexedDb, MigrationPhase = SyncStorageMigrationPhase.Copying };
        var sut = new SwitchableSyncFileStore("notes", mockResolver.Object);

        // Act & Assert
        await sut.ListAsync();
        await sut.ReadAsync("notes/a.md");
        Assert.Equal(SyncErrorCodes.StorageMigrating, (await Assert.ThrowsAsync<SyncStorageMigrationException>(() => sut.WriteAsync("notes/a.md", [1]))).Code);
        await Assert.ThrowsAsync<SyncStorageMigrationException>(() => sut.CreateFolderAsync("notes/Work/"));
        await Assert.ThrowsAsync<SyncStorageMigrationException>(() => sut.DeleteAsync("notes/a.md"));
        mockIndexedDb.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenSourceBeingCleared_WhenChanging_ThenAllowedOnTheTarget()
    {
        // Arrange
        state = new SyncStorageState { Kind = SyncStorageKind.Opfs, MigrationPhase = SyncStorageMigrationPhase.Cleanup };
        var sut = new SwitchableSyncFileStore("notes", mockResolver.Object);

        // Act
        await sut.WriteAsync("notes/a.md", [1]);

        // Assert
        mockOpfs.Verify(x => x.WriteAsync("notes/a.md", It.IsAny<byte[]>(), null, default), Times.Once);
    }
}
