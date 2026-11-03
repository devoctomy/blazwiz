using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.FileTree;
using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Folder;

public class FolderSyncFileStoreTests
{
    private const string Owner = "bob";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IndexedDbStore Metadata = FolderSyncFileStore.DescribeMetadataStore("sync", "notes_folder");

    private readonly Mock<IFolderAccessClient> mockFolder = new();
    private readonly Mock<IIndexedDbClient> mockIndexedDb = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly GlobSyncPathMatcher matcher = new("notes/**/*.md", includeEmptyFolders: true);

    public FolderSyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
        mockFolder.Setup(x => x.GetStatusAsync(Owner)).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = SyncFolderStatus.Granted });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("folderClient", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(null!, mockIndexedDb.Object, mockUser.Object, mockClock.Object, matcher, Metadata)).ParamName);
        Assert.Equal("indexedDbClient", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(mockFolder.Object, null!, mockUser.Object, mockClock.Object, matcher, Metadata)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(mockFolder.Object, mockIndexedDb.Object, null!, mockClock.Object, matcher, Metadata)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(mockFolder.Object, mockIndexedDb.Object, mockUser.Object, null!, matcher, Metadata)).ParamName);
        Assert.Equal("matcher", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(mockFolder.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, null!, Metadata)).ParamName);
        Assert.Equal("metadataStore", Assert.Throws<ArgumentNullException>(() => new FolderSyncFileStore(mockFolder.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, matcher, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeMetadataStore_ThenKeyedByOwnerAndKeyAndIndexedByOwner()
    {
        // Act & Assert
        Assert.Equal(("sync", "notes_folder"), (Metadata.Database, Metadata.Name));
        Assert.Equal(["owner", "key"], Metadata.KeyPath);
        Assert.Equal(["owner"], Assert.Single(Metadata.Indexes).KeyPath);
    }

    [Fact]
    public async Task GivenFolderWithOtherFiles_WhenListAsync_ThenOnlyProfileFilesUnderItsPrefix()
    {
        // Arrange
        var at = Now.ToUnixTimeMilliseconds();
        mockFolder.Setup(x => x.ListAsync(Owner, "notes/")).ReturnsAsync(
        [
            new FileTreeEntry { Key = "notes/a.md", Size = 3, LastModified = 100 },
            new FileTreeEntry { Key = "notes/edited.md", Size = 4, LastModified = at },
            new FileTreeEntry { Key = "notes/new.md", Size = 5, LastModified = at },
            new FileTreeEntry { Key = "notes/Work/", LastModified = at },
            new FileTreeEntry { Key = "notes/picture.png", LastModified = at },
        ]);
        mockIndexedDb.Setup(x => x.GetAllByIndexAsync<FolderFileRecord>(Metadata, "owner", Owner, null)).ReturnsAsync(
        [
            new FolderFileRecord { Key = "notes/a.md", ModifiedAt = Earlier, FileLastModified = 100 },
            new FolderFileRecord { Key = "notes/edited.md", ModifiedAt = Earlier, FileLastModified = 100 },
        ]);

        // Act
        var result = await CreateSut().ListAsync();

        // Assert
        Assert.Equal(["notes/a.md", "notes/edited.md", "notes/new.md", "notes/Work/"], result.Select(f => f.Key));
        Assert.Equal(Earlier, result[0].ModifiedAt);
        Assert.Equal(Now, result[1].ModifiedAt);
        Assert.Equal(Now, result[2].ModifiedAt);
        Assert.Equal(3, result[0].Size);
    }

    [Fact]
    public async Task GivenUnchangedFile_WhenReadAsync_ThenRecordedTime()
    {
        // Arrange
        mockFolder.Setup(x => x.ReadAsync(Owner, "notes/a.md")).ReturnsAsync(new FileTreeFile { Content = [1], LastModified = 100 });
        mockIndexedDb.Setup(x => x.GetAsync<FolderFileRecord>(Metadata, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { Owner, "notes/a.md" }))))
            .ReturnsAsync(new FolderFileRecord { ModifiedAt = Earlier, FileLastModified = 100 });

        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Equal([1], result!.Content);
        Assert.Equal(Earlier, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenFileEditedOutsideTheApp_WhenReadAsync_ThenFileSystemTime()
    {
        // Arrange
        mockFolder.Setup(x => x.ReadAsync(Owner, "notes/a.md")).ReturnsAsync(new FileTreeFile { LastModified = Now.ToUnixTimeMilliseconds() });
        mockIndexedDb.Setup(x => x.GetAsync<FolderFileRecord>(Metadata, It.IsAny<object>()))
            .ReturnsAsync(new FolderFileRecord { ModifiedAt = Earlier, FileLastModified = 100 });

        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Empty(result!.Content);
        Assert.Equal(Now, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenMissingFile_WhenReadAsync_ThenNull()
    {
        // Act & Assert
        Assert.Null(await CreateSut().ReadAsync("notes/a.md"));
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenFileWrittenThenTimesRecorded()
    {
        // Arrange
        mockFolder.Setup(x => x.WriteAsync(Owner, "notes/a.md", It.IsAny<byte[]>())).ReturnsAsync(200);

        // Act
        await CreateSut().WriteAsync("notes/a.md", [1], Earlier);

        // Assert
        mockIndexedDb.Verify(x => x.PutAsync(Metadata, It.Is<FolderFileRecord>(r => r.Owner == Owner && r.Key == "notes/a.md" && r.ModifiedAt == Earlier && r.FileLastModified == 200)), Times.Once);
    }

    [Fact]
    public async Task GivenFolderKey_WhenCreateFolderAsync_ThenEmptyMarkerWrittenNow()
    {
        // Act
        await CreateSut().CreateFolderAsync("notes/Work/");

        // Assert
        mockFolder.Verify(x => x.WriteAsync(Owner, "notes/Work/", It.Is<byte[]>(c => c.Length == 0)), Times.Once);
        mockIndexedDb.Verify(x => x.PutAsync(Metadata, It.Is<FolderFileRecord>(r => r.ModifiedAt == Now)), Times.Once);
    }

    [Fact]
    public async Task GivenKey_WhenDeleteAsync_ThenFileAndRecordDeleted()
    {
        // Act
        await CreateSut().DeleteAsync("notes/a.md");

        // Assert
        mockFolder.Verify(x => x.DeleteAsync(Owner, "notes/a.md"), Times.Once);
        mockIndexedDb.Verify(x => x.DeleteManyAsync(Metadata, It.Is<IEnumerable<object>>(k => ((object[])k.Single()).SequenceEqual(new object[] { Owner, "notes/a.md" }))), Times.Once);
    }

    [Fact]
    public async Task GivenNoFolderPicked_WhenUsed_ThenAccessExceptionAndNothingTouched()
    {
        // Arrange
        mockFolder.Setup(x => x.GetStatusAsync(Owner)).ReturnsAsync((SyncFolderStatus?)null);

        // Act
        var ex = await Assert.ThrowsAsync<SyncStorageAccessException>(() => CreateSut().ListAsync());

        // Assert
        Assert.Equal(SyncErrorCodes.FolderNotChosen, ex.Code);
        mockFolder.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenPermissionLapsed_WhenUsed_ThenAccessExceptionNamingTheFolder()
    {
        // Arrange
        mockFolder.Setup(x => x.GetStatusAsync(Owner)).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "prompt" });

        // Act
        var ex = await Assert.ThrowsAsync<SyncStorageAccessException>(() => CreateSut().WriteAsync("notes/a.md", [1]));

        // Assert
        Assert.Equal(SyncErrorCodes.FolderAccessNeeded, ex.Code);
        Assert.Equal(["Notes"], ex.Arguments);
        mockFolder.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenSignedOut_WhenUsed_ThenThrows()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ListAsync());
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ReadAsync(string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.WriteAsync(string.Empty, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteAsync("k", null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.CreateFolderAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateFolderAsync("notes/Work"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeleteAsync(string.Empty));
    }

    [Fact]
    public void GivenPermission_WhenIsGranted_ThenTrueOnlyForGranted()
    {
        // Act & Assert
        Assert.True(new SyncFolderStatus { Permission = "granted" }.IsGranted);
        Assert.False(new SyncFolderStatus { Permission = "prompt" }.IsGranted);
        Assert.False(new SyncFolderStatus().IsGranted);
    }

    [Fact]
    public void GivenEachConstructor_WhenCreatingAccessException_ThenMessageAndCauseKept()
    {
        // Arrange
        var cause = new InvalidOperationException("cause");

        // Act & Assert
        Assert.Equal("denied", new SyncStorageAccessException("denied").Code);
        Assert.Same(cause, new SyncStorageAccessException("denied", cause).InnerException);
    }

    private FolderSyncFileStore CreateSut() => new(mockFolder.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, matcher, Metadata);
}
