using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.FileTree;
using blazwiz.Core.Sync.Opfs;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Opfs;

public class OpfsSyncFileStoreTests
{
    private const string Owner = "bob";
    private const string Root = "notes_root";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IndexedDbStore Metadata = OpfsSyncFileStore.DescribeMetadataStore("blazwiz_sync", "notes_opfs");

    private readonly Mock<IOpfsClient> mockOpfs = new();
    private readonly Mock<IIndexedDbClient> mockIndexedDb = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISystemClock> mockClock = new();

    public OpfsSyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenNullOrBlankDependencies_WhenConstructing_ThenThrows()
    {
        Assert.Equal("opfsClient", Assert.Throws<ArgumentNullException>(() => new OpfsSyncFileStore(null!, mockIndexedDb.Object, mockUser.Object, mockClock.Object, Root, Metadata)).ParamName);
        Assert.Equal("indexedDbClient", Assert.Throws<ArgumentNullException>(() => new OpfsSyncFileStore(mockOpfs.Object, null!, mockUser.Object, mockClock.Object, Root, Metadata)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new OpfsSyncFileStore(mockOpfs.Object, mockIndexedDb.Object, null!, mockClock.Object, Root, Metadata)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new OpfsSyncFileStore(mockOpfs.Object, mockIndexedDb.Object, mockUser.Object, null!, Root, Metadata)).ParamName);
        Assert.Equal("root", Assert.Throws<ArgumentException>(() => new OpfsSyncFileStore(mockOpfs.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, " ", Metadata)).ParamName);
        Assert.Equal("metadataStore", Assert.Throws<ArgumentNullException>(() => new OpfsSyncFileStore(mockOpfs.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, Root, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeMetadataStore_ThenKeyedByOwnerAndKeyAndIndexedByOwner()
    {
        Assert.Equal(("blazwiz_sync", "notes_opfs"), (Metadata.Database, Metadata.Name));
        Assert.Equal(["owner", "key"], Metadata.KeyPath);
        var index = Assert.Single(Metadata.Indexes);
        Assert.Equal(OpfsSyncFileStore.OwnerIndex, index.Name);
        Assert.Equal(["owner"], index.KeyPath);
    }

    [Fact]
    public async Task GivenFilesWithAndWithoutMetadata_WhenListAsync_ThenRecordedTimeElseBrowserTime()
    {
        // Arrange
        mockOpfs.Setup(x => x.ListAsync(Root, Owner)).ReturnsAsync(
        [
            new FileTreeEntry { Key = "notes/a.md", Size = 3, LastModified = Now.ToUnixTimeMilliseconds() },
            new FileTreeEntry { Key = "notes/Work/", Size = 0, LastModified = Now.ToUnixTimeMilliseconds() },
        ]);
        mockIndexedDb.Setup(x => x.GetAllByIndexAsync<OpfsFileRecord>(Metadata, "owner", Owner, null))
            .ReturnsAsync([new OpfsFileRecord { Owner = Owner, Key = "notes/a.md", ModifiedAt = Earlier }]);

        // Act
        var result = await CreateSut().ListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal((Earlier, 3L), (result[0].ModifiedAt, result[0].Size));
        Assert.Equal(Now, result[1].ModifiedAt);
        Assert.True(result[1].IsFolder);
    }

    [Fact]
    public async Task GivenFileWithMetadata_WhenReadAsync_ThenContentAndRecordedTime()
    {
        // Arrange
        mockOpfs.Setup(x => x.ReadAsync(Root, Owner, "notes/a.md")).ReturnsAsync(new FileTreeFile { Content = [1, 2], LastModified = Now.ToUnixTimeMilliseconds() });
        mockIndexedDb.Setup(x => x.GetAsync<OpfsFileRecord>(Metadata, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { Owner, "notes/a.md" }))))
            .ReturnsAsync(new OpfsFileRecord { ModifiedAt = Earlier });

        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Equal("notes/a.md", result!.Key);
        Assert.Equal([1, 2], result.Content);
        Assert.Equal(Earlier, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenFileWithoutMetadataOrContent_WhenReadAsync_ThenEmptyContentAndBrowserTime()
    {
        // Arrange
        mockOpfs.Setup(x => x.ReadAsync(Root, Owner, "notes/a.md")).ReturnsAsync(new FileTreeFile { LastModified = Now.ToUnixTimeMilliseconds() });

        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Empty(result!.Content);
        Assert.Equal(Now, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenMissingFile_WhenReadAsync_ThenNullWithoutReadingMetadata()
    {
        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Null(result);
        mockIndexedDb.Verify(x => x.GetAsync<OpfsFileRecord>(It.IsAny<IndexedDbStore>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenFileWrittenBeforeMetadata()
    {
        // Arrange
        var calls = new List<string>();
        mockOpfs.Setup(x => x.WriteAsync(Root, Owner, "notes/a.md", It.Is<byte[]>(c => c.SequenceEqual(new byte[] { 1 }))))
            .Callback(() => calls.Add("file")).Returns(Task.CompletedTask);
        mockIndexedDb.Setup(x => x.PutAsync(Metadata, It.Is<OpfsFileRecord>(r => r.Owner == Owner && r.Key == "notes/a.md" && r.ModifiedAt == Earlier)))
            .Callback(() => calls.Add("metadata")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().WriteAsync("notes/a.md", [1], Earlier);

        // Assert
        Assert.Equal(["file", "metadata"], calls);
    }

    [Fact]
    public async Task GivenFolderKey_WhenCreateFolderAsync_ThenEmptyMarkerWrittenNow()
    {
        // Act
        await CreateSut().CreateFolderAsync("notes/Work/");

        // Assert
        mockOpfs.Verify(x => x.WriteAsync(Root, Owner, "notes/Work/", It.Is<byte[]>(c => c.Length == 0)), Times.Once);
        mockIndexedDb.Verify(x => x.PutAsync(Metadata, It.Is<OpfsFileRecord>(r => r.Key == "notes/Work/" && r.ModifiedAt == Now)), Times.Once);
    }

    [Fact]
    public async Task GivenKey_WhenDeleteAsync_ThenFileDeletedBeforeMetadata()
    {
        // Arrange
        var calls = new List<string>();
        mockOpfs.Setup(x => x.DeleteAsync(Root, Owner, "notes/a.md")).Callback(() => calls.Add("file")).Returns(Task.CompletedTask);
        mockIndexedDb.Setup(x => x.DeleteManyAsync(Metadata, It.Is<IEnumerable<object>>(k => ((object[])k.Single()).SequenceEqual(new object[] { Owner, "notes/a.md" }))))
            .Callback(() => calls.Add("metadata")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().DeleteAsync("notes/a.md");

        // Assert
        Assert.Equal(["file", "metadata"], calls);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        var sut = CreateSut();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ReadAsync(string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.WriteAsync(string.Empty, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteAsync("k", null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.CreateFolderAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateFolderAsync("notes/Work"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeleteAsync(string.Empty));
    }

    [Fact]
    public async Task GivenSignedOut_WhenListAsync_ThenThrows()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ListAsync());
    }

    private OpfsSyncFileStore CreateSut() => new(mockOpfs.Object, mockIndexedDb.Object, mockUser.Object, mockClock.Object, Root, Metadata);
}
