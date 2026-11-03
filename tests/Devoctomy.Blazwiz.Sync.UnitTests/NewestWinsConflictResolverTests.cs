using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class NewestWinsConflictResolverTests
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Eleven = Ten.AddHours(1);
    private static readonly DateTimeOffset Noon = Ten.AddHours(2);

    private readonly NewestWinsConflictResolver sut = new();

    [Fact]
    public void GivenLocalEditNewer_WhenResolve_ThenLocalWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten, LastModified = Noon });

        // Assert
        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenRemoteEditNewer_WhenResolve_ThenRemoteWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.EditEdit, Ten, new RemoteObjectInfo { ModifiedAt = Eleven, LastModified = Eleven });

        // Assert
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenStaleOfflineEditUploadedLater_WhenResolve_ThenEditTimeNotUploadTimeIsCompared()
    {
        // Act
        // Edited remotely at 10:00 on a device that was offline and uploaded at 12:00; edited locally at 11:00.
        var conflict = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten, LastModified = Noon });

        // Assert
        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenMissingMetadata_WhenResolve_ThenFallsBackToLastModified()
    {
        // Arrange
        var olderUpload = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { LastModified = Ten });

        // Act
        var newerUpload = Conflict(SyncConflictKind.EditEdit, Eleven, new RemoteObjectInfo { LastModified = Noon });

        // Assert
        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(olderUpload));
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(newerUpload));
    }

    [Fact]
    public void GivenSameTimes_WhenResolve_ThenRemoteWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.EditEdit, Ten, new RemoteObjectInfo { ModifiedAt = Ten });

        // Assert
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenLocalDeleteNewerThanRemoteEdit_WhenResolve_ThenLocalDeleteWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.DeleteEdit, Eleven, new RemoteObjectInfo { ModifiedAt = Ten });

        // Assert
        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenRemoteEditNewerThanLocalDelete_WhenResolve_ThenRemoteEditWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.DeleteEdit, Ten, new RemoteObjectInfo { ModifiedAt = Eleven });

        // Assert
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenLocalEditAndRemoteDelete_WhenResolve_ThenLocalEditWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.EditDelete, Ten, null);

        // Assert
        Assert.Equal(SyncConflictWinner.Local, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenNoLocalTime_WhenResolve_ThenRemoteWins()
    {
        // Act
        var conflict = Conflict(SyncConflictKind.DeleteEdit, null, new RemoteObjectInfo { ModifiedAt = Ten });

        // Assert
        Assert.Equal(SyncConflictWinner.Remote, sut.Resolve(conflict));
    }

    [Fact]
    public void GivenNull_WhenResolve_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Resolve(null!));
    }

    private static SyncConflict Conflict(SyncConflictKind kind, DateTimeOffset? local, RemoteObjectInfo? remote) =>
        new() { Key = "notes/a.md", Kind = kind, LocalChangedAt = local, Remote = remote };
}
