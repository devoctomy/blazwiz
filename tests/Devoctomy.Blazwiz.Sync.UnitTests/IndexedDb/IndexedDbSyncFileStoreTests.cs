using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.IndexedDb;

public class IndexedDbSyncFileStoreTests
{
    private const string Owner = "bob";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Database = "notes";
    private static readonly IndexedDbStore Store = IndexedDbSyncFileStore.DescribeStore(Database, "files");

    private readonly Mock<IIndexedDbClient> mockClient = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<TimeProvider> mockClock = new();

    public IndexedDbSyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncFileStore(null!, mockUser.Object, mockClock.Object, Store)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncFileStore(mockClient.Object, null!, mockClock.Object, Store)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncFileStore(mockClient.Object, mockUser.Object, null!, Store)).ParamName);
        Assert.Equal("store", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncFileStore(mockClient.Object, mockUser.Object, mockClock.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeStore_ThenKeyedByOwnerAndKeyAndIndexedByOwner()
    {
        // Act & Assert
        Assert.Equal(Database, Store.Database);
        Assert.Equal("files", Store.Name);
        Assert.Equal(["owner", "key"], Store.KeyPath);
        var index = Assert.Single(Store.Indexes);
        Assert.Equal(IndexedDbSyncFileStore.OwnerIndex, index.Name);
        Assert.Equal(["owner"], index.KeyPath);
    }

    [Fact]
    public async Task GivenOwnersFiles_WhenListAsync_ThenReadsByOwnerWithoutContent()
    {
        // Arrange
        mockClient.Setup(x => x.GetAllByIndexAsync<IndexedDbFileRecord>(Store, "owner", Owner, It.Is<IReadOnlyList<string>>(f => f.SequenceEqual(new[] { "content" }))))
            .ReturnsAsync([new IndexedDbFileRecord { Key = "notes/a.md", ModifiedAt = Now, Size = 3 }]);

        // Act
        var result = await CreateSut().ListAsync();

        // Assert
        var file = Assert.Single(result);
        Assert.Equal("notes/a.md", file.Key);
        Assert.Equal(Now, file.ModifiedAt);
        Assert.Equal(3, file.Size);
    }

    [Fact]
    public async Task GivenFile_WhenReadAsync_ThenReadsByOwnerAndKey()
    {
        // Arrange
        mockClient.Setup(x => x.GetAsync<IndexedDbFileRecord>(Store, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { Owner, "notes/a.md" }))))
            .ReturnsAsync(new IndexedDbFileRecord { Key = "notes/a.md", Content = [1, 2], ModifiedAt = Now });

        // Act
        var result = await CreateSut().ReadAsync("notes/a.md");

        // Assert
        Assert.Equal([1, 2], result!.Content);
        Assert.Equal(Now, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenMissingFileOrContent_WhenReadAsync_ThenNullOrEmpty()
    {
        // Arrange
        mockClient.Setup(x => x.GetAsync<IndexedDbFileRecord>(Store, It.IsAny<object>())).ReturnsAsync((IndexedDbFileRecord?)null);
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.ReadAsync("notes/missing.md"));

        mockClient.Setup(x => x.GetAsync<IndexedDbFileRecord>(Store, It.IsAny<object>())).ReturnsAsync(new IndexedDbFileRecord { Key = "notes/Work/" });
        Assert.Empty((await sut.ReadAsync("notes/Work/"))!.Content);
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenPutsRecordWithOwnerSizeAndTime()
    {
        // Arrange
        IndexedDbFileRecord? saved = null;
        mockClient.Setup(x => x.PutAsync(Store, It.IsAny<IndexedDbFileRecord>())).Callback<IndexedDbStore, IndexedDbFileRecord>((_, r) => saved = r).Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Act
        await sut.WriteAsync("notes/a.md", [1, 2, 3]);

        // Assert
        Assert.Equal(Owner, saved!.Owner);
        Assert.Equal("notes/a.md", saved.Key);
        Assert.Equal([1, 2, 3], saved.Content);
        Assert.Equal(3, saved.Size);
        Assert.Equal(Now, saved.ModifiedAt);
    }

    [Fact]
    public async Task GivenModifiedTime_WhenWriteAsync_ThenItIsKept()
    {
        // Arrange
        IndexedDbFileRecord? saved = null;
        mockClient.Setup(x => x.PutAsync(Store, It.IsAny<IndexedDbFileRecord>())).Callback<IndexedDbStore, IndexedDbFileRecord>((_, r) => saved = r).Returns(Task.CompletedTask);

        // Act
        await CreateSut().WriteAsync("notes/a.md", [1], DateTimeOffset.UnixEpoch);

        // Assert
        Assert.Equal(DateTimeOffset.UnixEpoch, saved!.ModifiedAt);
    }

    [Fact]
    public async Task GivenFolderKey_WhenCreateFolderAsync_ThenEmptyRecordPut()
    {
        // Arrange
        IndexedDbFileRecord? saved = null;
        mockClient.Setup(x => x.PutAsync(Store, It.IsAny<IndexedDbFileRecord>())).Callback<IndexedDbStore, IndexedDbFileRecord>((_, r) => saved = r).Returns(Task.CompletedTask);

        // Act
        await CreateSut().CreateFolderAsync("notes/Work/");

        // Assert
        Assert.Equal("notes/Work/", saved!.Key);
        Assert.Empty(saved.Content!);
    }

    [Fact]
    public async Task GivenKeyWithoutSlash_WhenCreateFolderAsync_ThenThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().CreateFolderAsync("notes/Work"));
    }

    [Fact]
    public async Task GivenFile_WhenDeleteAsync_ThenDeletesByOwnerAndKey()
    {
        // Act
        await CreateSut().DeleteAsync("notes/a.md");

        // Assert
        mockClient.Verify(x => x.DeleteManyAsync(Store, It.Is<IEnumerable<object>>(k => ((object[])k.Single()).SequenceEqual(new object[] { Owner, "notes/a.md" }))), Times.Once);
    }

    [Fact]
    public async Task GivenSignedOut_WhenListAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ListAsync());
    }

    private IndexedDbSyncFileStore CreateSut() => new(mockClient.Object, mockUser.Object, mockClock.Object, Store);
}
