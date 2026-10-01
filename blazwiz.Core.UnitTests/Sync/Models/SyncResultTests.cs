using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync.Models;

public class SyncResultTests
{
    [Fact]
    public void GivenNoChanges_WhenDescribe_ThenUpToDate()
    {
        var sut = new SyncResult();

        Assert.False(sut.HasChanges);
        Assert.Equal("Up to date", sut.Describe());
    }

    [Fact]
    public void GivenCounts_WhenDescribe_ThenListsEachNonZeroCount()
    {
        var sut = new SyncResult { Uploaded = 3, Downloaded = 1, DeletedLocal = 1, DeletedRemote = 1, ConflictsResolved = 1, Failed = 2 };

        Assert.True(sut.HasChanges);
        Assert.Equal("3 uploaded, 1 downloaded, 2 deleted, 1 conflict resolved, 2 failed", sut.Describe());
        sut.ConflictsResolved = 2;
        Assert.Contains("2 conflicts resolved", sut.Describe(), StringComparison.Ordinal);
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
