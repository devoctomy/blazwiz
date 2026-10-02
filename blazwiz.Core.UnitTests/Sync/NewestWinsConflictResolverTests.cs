using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync;

public class NewestWinsConflictResolverTests
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Eleven = Ten.AddHours(1);
    private static readonly DateTimeOffset Noon = Ten.AddHours(2);

    private readonly NewestWinsConflictResolver sut = new();

    [Fact]
    public void GivenLocalEditNewer_WhenResolve_ThenLocalWins()
    {
        var conflict = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten, LastModified = Noon });

        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenRemoteEditNewer_WhenResolve_ThenRemoteWins()
    {
        var conflict = Conflict(SyncConflictKind.EditEdit, Ten, new RemoteObjectInfo { ModifiedAt = Eleven, LastModified = Eleven });

        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenStaleOfflineEditUploadedLater_WhenResolve_ThenEditTimeNotUploadTimeIsCompared()
    {
        // Edited remotely at 10:00 on a device that was offline and uploaded at 12:00; edited locally at 11:00.
        var conflict = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten, LastModified = Noon });

        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenMissingMetadata_WhenResolve_ThenFallsBackToLastModified()
    {
        var olderUpload = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { LastModified = Ten });
        var newerUpload = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { LastModified = Noon });

        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(olderUpload));
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(newerUpload));
    }

    [Fact]
    public void GivenSameTimes_WhenResolve_ThenRemoteWins()
    {
        var conflict = Conflict(SyncConflictKind.EditEdit, Ten, new RemoteObjectInfo { ModifiedAt = Ten });

        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenLocalDeleteNewerThanRemoteEdit_WhenResolve_ThenLocalDeleteWins()
    {
        var conflict = Conflict(SyncConflictKind.DeleteEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten });

        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenRemoteEditNewerThanLocalDelete_WhenResolve_ThenRemoteEditWins()
    {
        var conflict = Conflict(SyncConflictKind.DeleteEdit, Ten, new RemoteObjectInfo { ModifiedAt = Eleven });

        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenLocalEditAndRemoteDelete_WhenResolve_ThenLocalEditWins()
    {
        var conflict = Conflict(SyncConflictKind.EditDelete, Ten, null);

        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenNoLocalTime_WhenResolve_ThenRemoteWins()
    {
        var conflict = Conflict(SyncConflictKind.DeleteEdit, null, new RemoteObjectInfo { ModifiedAt = Ten });

        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenNull_WhenResolve_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => sut.Resolve(null!));
    }

    private static SyncConflict Conflict(SyncConflictKind kind, DateTimeOffset? local, RemoteObjectInfo? remote) =>
        new() { Key = "notes/a.md", Kind = kind, LocalChangedAt = local, Remote = remote };
}
