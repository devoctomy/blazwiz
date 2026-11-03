using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Models;

public class SyncResultTests
{
    [Fact]
    public void GivenCounts_WhenHasChanges_ThenTrueOnlyForChanges()
    {
        // Act & Assert
        Assert.False(new SyncResult().HasChanges);
        Assert.False(new SyncResult { Adopted = 2, Failed = 1 }.HasChanges);
        Assert.True(new SyncResult { DeletedLocal = 1 }.HasChanges);
    }

    [Fact]
    public void GivenTwoResults_WhenAdd_ThenCountsErrorsAndKeysCombined()
    {
        // Arrange
        var sut = new SyncResult { Uploaded = 1 };
        sut.AddChangedLocalKey("notes", "notes/a.md");
        var other = new SyncResult { Uploaded = 2, Downloaded = 1, DeletedRemote = 1, DeletedLocal = 1, Adopted = 1, ConflictsResolved = 1, Failed = 1 };
        other.Errors.Add("boom");
        other.AddChangedLocalKey("notes", "notes/b.md");
        other.AddChangedLocalKey("photos", "photos/c.png");

        // Act
        sut.Add(other);

        // Assert
        Assert.Equal(3, sut.Uploaded);
        Assert.Equal(1, sut.Downloaded);
        Assert.Equal(1, sut.DeletedRemote);
        Assert.Equal(1, sut.DeletedLocal);
        Assert.Equal(1, sut.Adopted);
        Assert.Equal(1, sut.ConflictsResolved);
        Assert.Equal(1, sut.Failed);
        Assert.Equal(["boom"], sut.Errors);
        Assert.Equal(["notes/a.md", "notes/b.md"], sut.ChangedLocalKeys["notes"]);
        Assert.Equal(["photos/c.png"], sut.ChangedLocalKeys["photos"]);
        Assert.Throws<ArgumentNullException>(() => sut.Add(null!));
    }
}
