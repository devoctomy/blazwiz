using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync;

public class SyncPlannerTests
{
    private const string Key = "notes/a.md";
    private const string BaseETag = "\"base\"";
    private const string BaseHash = "hash-base";

    private readonly SyncPlanner sut = new();

    public enum LocalState
    {
        Absent,
        New,
        Unchanged,
        ChangedDirty,
        ChangedHash,
        Tombstone,
        TombstoneNoBase,
    }

    public enum RemoteState
    {
        Absent,
        New,
        Unchanged,
        Changed,
    }

    public static TheoryData<LocalState, RemoteState, SyncActionKind, SyncConflictKind?> DecisionTable => new()
    {
        { LocalState.Unchanged, RemoteState.Unchanged, SyncActionKind.None, null },
        { LocalState.ChangedDirty, RemoteState.Unchanged, SyncActionKind.Upload, null },
        { LocalState.ChangedHash, RemoteState.Unchanged, SyncActionKind.Upload, null },
        { LocalState.Unchanged, RemoteState.Changed, SyncActionKind.Download, null },
        { LocalState.ChangedDirty, RemoteState.Changed, SyncActionKind.Conflict, SyncConflictKind.EditEdit },
        { LocalState.New, RemoteState.Absent, SyncActionKind.UploadNew, null },
        { LocalState.Absent, RemoteState.New, SyncActionKind.DownloadNew, null },
        { LocalState.New, RemoteState.New, SyncActionKind.CompareContent, null },
        { LocalState.Tombstone, RemoteState.Unchanged, SyncActionKind.DeleteRemote, null },
        { LocalState.Tombstone, RemoteState.Changed, SyncActionKind.Conflict, SyncConflictKind.DeleteEdit },
        { LocalState.Tombstone, RemoteState.Absent, SyncActionKind.DropRecord, null },
        { LocalState.TombstoneNoBase, RemoteState.Absent, SyncActionKind.DropRecord, null },
        { LocalState.TombstoneNoBase, RemoteState.New, SyncActionKind.Conflict, SyncConflictKind.DeleteEdit },
        { LocalState.Unchanged, RemoteState.Absent, SyncActionKind.DeleteLocal, null },
        { LocalState.ChangedDirty, RemoteState.Absent, SyncActionKind.Conflict, SyncConflictKind.EditDelete },
        { LocalState.Absent, RemoteState.Absent, SyncActionKind.None, null },
    };

    [Theory]
    [MemberData(nameof(DecisionTable))]
    public void GivenStateOfBothSides_WhenPlanItem_ThenActionMatchesDecisionTable(LocalState local, RemoteState remote, SyncActionKind expected, SyncConflictKind? expectedConflict)
    {
        // Arrange
        var item = BuildItem(local, remote);

        // Act
        var result = sut.PlanItem(item);

        // Assert
        Assert.Equal(expected, result.Kind);
        Assert.Equal(expectedConflict, result.ConflictKind);
        Assert.Equal(Key, result.Key);
        Assert.Same(item.Local, result.Local);
        Assert.Same(item.Remote, result.Remote);
        Assert.Same(item.Record, result.Record);
    }

    [Fact]
    public void GivenLocalCopyMissingWithoutTombstone_WhenPlanItem_ThenDownloadsAgain()
    {
        // Arrange
        var item = new SyncPlanItem { Key = Key, Record = Synced(), Remote = Remote(BaseETag) };

        // Act
        var result = sut.PlanItem(item);

        // Assert
        Assert.Equal(SyncActionKind.Download, result.Kind);
    }

    [Fact]
    public void GivenRecordGoneFromBothSides_WhenPlanItem_ThenDropsRecord()
    {
        // Arrange
        var item = new SyncPlanItem { Key = Key, Record = Synced() };

        // Act
        var result = sut.PlanItem(item);

        // Assert
        Assert.Equal(SyncActionKind.DropRecord, result.Kind);
    }

    [Fact]
    public void GivenVerifyHashEqualToBase_WhenPlanItem_ThenNothingToDo()
    {
        // Arrange
        var item = new SyncPlanItem { Key = Key, Local = Local(), LocalHash = BaseHash, Record = Synced(), Remote = Remote(BaseETag) };

        // Act
        var result = sut.PlanItem(item);

        // Assert
        Assert.Equal(SyncActionKind.None, result.Kind);
    }

    [Fact]
    public void GivenKeysOnEitherSideAndInIndex_WhenPlan_ThenOneActionPerKeyInKeyOrder()
    {
        // Arrange
        var local = new[] { new SyncFileInfo { Key = "notes/c.md" }, new SyncFileInfo { Key = "notes/a.md" } };
        var remote = new[] { new RemoteObjectInfo { Key = "notes/b.md", ETag = "1" }, new RemoteObjectInfo { Key = "notes/a.md", ETag = "2" } };
        var index = new[] { new SyncIndexRecord { Key = "notes/d.md", BaseETag = "3", Deleted = true } };

        // Act
        var result = sut.Plan(local, remote, index, null);

        // Assert
        Assert.Equal(["notes/a.md", "notes/b.md", "notes/c.md", "notes/d.md"], result.Actions.Select(a => a.Key));
        Assert.Equal(
            [SyncActionKind.CompareContent, SyncActionKind.DownloadNew, SyncActionKind.UploadNew, SyncActionKind.DropRecord],
            result.Actions.Select(a => a.Kind));
    }

    [Fact]
    public void GivenLocalHashes_WhenPlan_ThenHashesAreUsedToDetectChanges()
    {
        // Arrange
        var local = new[] { Local() };
        var remote = new[] { Remote(BaseETag) };
        var index = new[] { Synced() };
        var hashes = new Dictionary<string, string> { [Key] = "different" };

        // Act
        var result = sut.Plan(local, remote, index, hashes);

        // Assert
        Assert.Equal(SyncActionKind.Upload, Assert.Single(result.Actions).Kind);
    }

    [Fact]
    public void GivenNullArguments_WhenPlan_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => sut.Plan(null!, [], [], null));
        Assert.Throws<ArgumentNullException>(() => sut.Plan([], null!, [], null));
        Assert.Throws<ArgumentNullException>(() => sut.Plan([], [], null!, null));
        Assert.Throws<ArgumentNullException>(() => sut.PlanItem(null!));
    }

    private static SyncPlanItem BuildItem(LocalState local, RemoteState remote)
    {
        var item = new SyncPlanItem { Key = Key };
        switch (local)
        {
            case LocalState.New:
                item.Local = Local();
                item.Record = new SyncIndexRecord { Key = Key, Dirty = true, Revision = 1 };
                break;
            case LocalState.Unchanged:
                item.Local = Local();
                item.Record = Synced();
                break;
            case LocalState.ChangedDirty:
                item.Local = Local();
                item.Record = Synced();
                item.Record.Dirty = true;
                break;
            case LocalState.ChangedHash:
                item.Local = Local();
                item.Record = Synced();
                item.LocalHash = "edited-outside-the-tracker";
                break;
            case LocalState.Tombstone:
                item.Record = Synced();
                item.Record.Deleted = true;
                item.Record.DeletedAt = DateTimeOffset.UnixEpoch;
                break;
            case LocalState.TombstoneNoBase:
                item.Record = new SyncIndexRecord { Key = Key, Deleted = true, DeletedAt = DateTimeOffset.UnixEpoch };
                break;
        }

        item.Remote = remote switch
        {
            RemoteState.New => Remote("\"new\""),
            RemoteState.Unchanged => Remote(BaseETag),
            RemoteState.Changed => Remote("\"changed\""),
            _ => null,
        };

        // "Absent (had base)" needs a record with a base; "new" needs none.
        if (local == LocalState.Absent && remote == RemoteState.Absent)
        {
            item.Record = null;
        }

        return item;
    }

    private static SyncFileInfo Local() => new() { Key = Key, ModifiedAt = DateTimeOffset.UnixEpoch, Size = 1 };

    private static RemoteObjectInfo Remote(string etag) => new() { Key = Key, ETag = etag };

    private static SyncIndexRecord Synced() => new() { Key = Key, BaseETag = BaseETag, BaseHash = BaseHash, Revision = 1 };
}
