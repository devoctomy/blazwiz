using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.IndexedDb;

public class IndexedDbSyncStorageStateStoreTests
{
    private static readonly IndexedDbStore Store = IndexedDbSyncStorageStateStore.DescribeStore("sync", "storage");

    private readonly Mock<IIndexedDbClient> mockClient = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncStorageStateStore(null!, Store)).ParamName);
        Assert.Equal("store", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncStorageStateStore(mockClient.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeStore_ThenKeyedByProfileAndOwner()
    {
        // Act & Assert
        Assert.Equal(("sync", "storage"), (Store.Database, Store.Name));
        Assert.Equal(["profile", "owner"], Store.KeyPath);
        Assert.Empty(Store.Indexes);
    }

    [Fact]
    public async Task GivenState_WhenGetAsync_ThenReadsByProfileAndOwner()
    {
        // Arrange
        var state = new SyncStorageState { Profile = "notes", Owner = "bob", Kind = SyncStorageKind.Opfs };
        mockClient.Setup(x => x.GetAsync<SyncStorageState>(Store, It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { "notes", "bob" }))))
            .ReturnsAsync(state);

        // Act & Assert
        Assert.Same(state, await CreateSut().GetAsync("notes", "bob"));
    }

    [Fact]
    public async Task GivenState_WhenSaveAsync_ThenPutsIt()
    {
        // Arrange
        var state = new SyncStorageState { Profile = "notes", Owner = "bob" };

        // Act
        await CreateSut().SaveAsync(state);

        // Assert
        mockClient.Verify(x => x.PutAsync(Store, state), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync(string.Empty, "bob"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync("notes", string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }

    [Fact]
    public void GivenPhase_WhenIsCopying_ThenTrueOnlyWhileCopying()
    {
        // Act & Assert
        Assert.True(new SyncStorageState { MigrationPhase = SyncStorageMigrationPhase.Copying }.IsCopying);
        Assert.False(new SyncStorageState { MigrationPhase = SyncStorageMigrationPhase.Cleanup }.IsCopying);
        Assert.False(new SyncStorageState().IsCopying);
    }

    private IndexedDbSyncStorageStateStore CreateSut() => new(mockClient.Object, Store);
}
