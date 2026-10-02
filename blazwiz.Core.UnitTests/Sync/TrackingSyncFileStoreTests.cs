using blazwiz.Core.Sync;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync;

public class TrackingSyncFileStoreTests
{
    private const string Owner = "bob";
    private const string Profile = "notes";
    private const string Key = "notes/a.md";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISyncFileStore> mockInner = new();
    private readonly InMemorySyncIndexStore index = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<ISystemClock> mockClock = new();

    public TrackingSyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenInvalidArguments_WhenConstructing_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => new TrackingSyncFileStore(" ", mockInner.Object, index, mockUser.Object, mockScheduler.Object, mockClock.Object));
        Assert.Equal("inner", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, null!, index, mockUser.Object, mockScheduler.Object, mockClock.Object)).ParamName);
        Assert.Equal("indexStore", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, null!, mockUser.Object, mockScheduler.Object, mockClock.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, null!, mockScheduler.Object, mockClock.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, mockUser.Object, null!, mockClock.Object)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, mockUser.Object, mockScheduler.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenWrite_WhenWriteAsync_ThenFileWrittenFirstThenMarkedDirtyAndSyncRequested()
    {
        // Arrange
        var order = new List<string>();
        mockInner.Setup(x => x.WriteAsync(Key, It.IsAny<byte[]>(), null, default)).Callback(() => order.Add("file")).Returns(Task.CompletedTask);
        mockScheduler.Setup(x => x.RequestSync(Profile)).Callback(() => order.Add(index.Records.Single().Dirty ? "dirty+sync" : "sync"));
        var sut = CreateSut();

        // Act
        await sut.WriteAsync(Key, [1, 2]);

        // Assert
        Assert.Equal(["file", "dirty+sync"], order);
        var record = Assert.Single(index.Records);
        Assert.Equal(Profile, record.Profile);
        Assert.Equal(Owner, record.Owner);
        Assert.Equal(1, record.Revision);
        Assert.False(record.Deleted);
    }

    [Fact]
    public async Task GivenSyncedFile_WhenWriteAsync_ThenBaseIsKeptAndRevisionIncreases()
    {
        // Arrange
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = Key, BaseETag = "e", BaseHash = "h", Revision = 4 });
        var sut = CreateSut();

        // Act
        await sut.WriteAsync(Key, [1]);

        // Assert
        var record = Assert.Single(index.Records);
        Assert.Equal("e", record.BaseETag);
        Assert.Equal("h", record.BaseHash);
        Assert.True(record.Dirty);
        Assert.Equal(5, record.Revision);
    }

    [Fact]
    public async Task GivenFile_WhenDeleteAsync_ThenTombstoneLeftAndSyncRequested()
    {
        // Arrange
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = Key, BaseETag = "e", Dirty = true, Revision = 1 });
        var sut = CreateSut();

        // Act
        await sut.DeleteAsync(Key);

        // Assert
        mockInner.Verify(x => x.DeleteAsync(Key, default), Times.Once);
        var record = Assert.Single(index.Records);
        Assert.True(record.Deleted);
        Assert.False(record.Dirty);
        Assert.Equal(Now, record.DeletedAt);
        Assert.Equal(2, record.Revision);
        mockScheduler.Verify(x => x.RequestSync(Profile), Times.Once);
    }

    [Fact]
    public async Task GivenTombstone_WhenWriteAsync_ThenTombstoneCleared()
    {
        // Arrange
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = Key, Deleted = true, DeletedAt = Now });
        var sut = CreateSut();

        // Act
        await sut.WriteAsync(Key, [1]);

        // Assert
        var record = Assert.Single(index.Records);
        Assert.False(record.Deleted);
        Assert.Null(record.DeletedAt);
        Assert.True(record.Dirty);
    }

    [Fact]
    public async Task GivenFolder_WhenCreateFolderAsync_ThenMarkerTracked()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.CreateFolderAsync("notes/Work/");

        // Assert
        mockInner.Verify(x => x.CreateFolderAsync("notes/Work/", default), Times.Once);
        Assert.True(Assert.Single(index.Records).Dirty);
    }

    [Fact]
    public async Task GivenReads_WhenListAndReadAsync_ThenPassedThroughWithoutTracking()
    {
        // Arrange
        var list = new List<SyncFileInfo> { new() { Key = Key } };
        var file = new SyncFile { Key = Key };
        mockInner.Setup(x => x.ListAsync(default)).ReturnsAsync(list);
        mockInner.Setup(x => x.ReadAsync(Key, default)).ReturnsAsync(file);
        var sut = CreateSut();

        // Act & Assert
        Assert.Same(list, await sut.ListAsync());
        Assert.Same(file, await sut.ReadAsync(Key));
        Assert.Empty(index.Records);
        mockScheduler.Verify(x => x.RequestSync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSignedOut_WhenWriteAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().WriteAsync(Key, [1]));
    }

    private TrackingSyncFileStore CreateSut() =>
        new(Profile, mockInner.Object, index, mockUser.Object, mockScheduler.Object, mockClock.Object);
}
