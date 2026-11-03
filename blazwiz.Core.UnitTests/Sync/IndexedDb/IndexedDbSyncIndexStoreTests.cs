using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.IndexedDb;

public class IndexedDbSyncIndexStoreTests
{
    private static readonly IndexedDbStore Store = IndexedDbSyncIndexStore.DescribeStore("sync", "notes_index");

    private readonly Mock<IIndexedDbClient> mockClient = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncIndexStore(null!, Store)).ParamName);
        Assert.Equal("store", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncIndexStore(mockClient.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeStore_ThenKeyedByProfileOwnerAndKey()
    {
        Assert.Equal(["profile", "owner", "key"], Store.KeyPath);
        var index = Assert.Single(Store.Indexes);
        Assert.Equal(["profile", "owner"], index.KeyPath);
    }

    [Fact]
    public async Task GivenProfileAndOwner_WhenGetAllAsync_ThenReadsCompoundIndex()
    {
        // Arrange
        var records = new List<SyncIndexRecord> { new() { Key = "notes/a.md" } };
        mockClient.Setup(x => x.GetAllByIndexAsync<SyncIndexRecord>(Store, IndexedDbSyncIndexStore.ProfileOwnerIndex, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { "notes", "bob" })), null))
            .ReturnsAsync(records);

        // Act & Assert
        Assert.Same(records, await new IndexedDbSyncIndexStore(mockClient.Object, Store).GetAllAsync("notes", "bob"));
    }

    [Fact]
    public async Task GivenKey_WhenGetAsync_ThenReadsByCompoundKey()
    {
        // Arrange
        var record = new SyncIndexRecord { Key = "notes/a.md" };
        mockClient.Setup(x => x.GetAsync<SyncIndexRecord>(Store, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { "notes", "bob", "notes/a.md" }))))
            .ReturnsAsync(record);

        // Act & Assert
        Assert.Same(record, await new IndexedDbSyncIndexStore(mockClient.Object, Store).GetAsync("notes", "bob", "notes/a.md"));
    }

    [Fact]
    public async Task GivenRecord_WhenSaveAsync_ThenPutsIt()
    {
        // Arrange
        var record = new SyncIndexRecord { Key = "notes/a.md" };

        // Act
        await new IndexedDbSyncIndexStore(mockClient.Object, Store).SaveAsync(record);

        // Assert
        mockClient.Verify(x => x.PutAsync(Store, record), Times.Once);
    }

    [Fact]
    public async Task GivenKey_WhenDeleteAsync_ThenDeletesByCompoundKey()
    {
        // Act
        await new IndexedDbSyncIndexStore(mockClient.Object, Store).DeleteAsync("notes", "bob", "notes/a.md");

        // Assert
        mockClient.Verify(x => x.DeleteManyAsync(Store, It.Is<IEnumerable<object>>(k => ((object[])k.Single()).SequenceEqual(new object[] { "notes", "bob", "notes/a.md" }))), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        var sut = new IndexedDbSyncIndexStore(mockClient.Object, Store);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAllAsync(string.Empty, "bob"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync("notes", string.Empty, "k"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeleteAsync("notes", "bob", string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }
}
