using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

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
    private readonly Mock<TimeProvider> mockClock = new();

    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());

    public TrackingSyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
    }

    [Fact]
    public void GivenInvalidArguments_WhenConstructing_ThenThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TrackingSyncFileStore(" ", mockInner.Object, index, mockUser.Object, mockScheduler.Object, mockClock.Object, format));
        Assert.Equal("inner", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, null!, index, mockUser.Object, mockScheduler.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("indexStore", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, null!, mockUser.Object, mockScheduler.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, null!, mockScheduler.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, mockUser.Object, null!, mockClock.Object, format)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, mockUser.Object, mockScheduler.Object, null!, format)).ParamName);
        Assert.Equal("encryptedFileFormat", Assert.Throws<ArgumentNullException>(() => new TrackingSyncFileStore(Profile, mockInner.Object, index, mockUser.Object, mockScheduler.Object, mockClock.Object, null!)).ParamName);
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

    [Fact]
    public async Task GivenEncryptedContent_WhenWriteAsync_ThenIndexRecordsItsKey()
    {
        // Arrange
        var sut = CreateSut();
        var encrypted = format.Encrypt([1, 2], "k_1", new byte[32], new E2eFileBinding("s1", "x"));

        // Act
        await sut.WriteAsync(Key, encrypted);

        // Assert
        var record = Assert.Single(index.Records);
        Assert.True(record.Encrypted);
        Assert.Equal("k_1", record.KeyId);
    }

    [Fact]
    public async Task GivenEncryptedFileRewrittenPlain_WhenWriteAsync_ThenIndexKeyCleared()
    {
        // Arrange
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = Key, Encrypted = true, KeyId = "k_1" });
        var sut = CreateSut();

        // Act
        await sut.WriteAsync(Key, [1, 2]);

        // Assert
        var record = Assert.Single(index.Records);
        Assert.False(record.Encrypted);
        Assert.Null(record.KeyId);
    }

    [Fact]
    public async Task GivenIndexedKeys_WhenListAsync_ThenEachFileHasItsEncryptionKeyId()
    {
        // Arrange
        mockInner.Setup(x => x.ListAsync(default)).ReturnsAsync([new SyncFileInfo { Key = Key }, new SyncFileInfo { Key = "notes/b.md" }]);
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = Key, Encrypted = true, KeyId = "k_1" });

        // Act
        var files = await CreateSut().ListAsync();

        // Assert
        Assert.Equal("k_1", files.Single(f => f.Key == Key).EncryptionKeyId);
        Assert.Null(files.Single(f => f.Key == "notes/b.md").EncryptionKeyId);
    }

    private TrackingSyncFileStore CreateSut() =>
        new(Profile, mockInner.Object, index, mockUser.Object, mockScheduler.Object, mockClock.Object, format);
}
