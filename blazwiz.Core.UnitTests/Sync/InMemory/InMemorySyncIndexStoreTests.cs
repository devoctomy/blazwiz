using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync.InMemory;

public class InMemorySyncIndexStoreTests
{
    [Fact]
    public async Task GivenRecords_WhenSavedReadAndDeleted_ThenCopiesAreKeptPerProfileAndOwner()
    {
        // Arrange
        var sut = new InMemorySyncIndexStore();
        var record = new SyncIndexRecord { Profile = "notes", Owner = "bob", Key = "notes/a.md", Dirty = true };
        await sut.SaveAsync(record);
        await sut.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = "alice", Key = "notes/a.md" });

        // Act
        record.Dirty = false;
        var all = await sut.GetAllAsync("notes", "bob");
        var one = await sut.GetAsync("notes", "bob", "notes/a.md");
        await sut.DeleteAsync("notes", "bob", "notes/a.md");

        // Assert
        Assert.True(Assert.Single(all).Dirty);
        Assert.True(one!.Dirty);
        Assert.Null(await sut.GetAsync("notes", "bob", "notes/a.md"));
        Assert.Single(sut.Records);

        sut.Clear();
        Assert.Empty(sut.Records);
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }
}
