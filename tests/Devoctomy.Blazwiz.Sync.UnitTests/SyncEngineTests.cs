using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.UnitTests.Fakes;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class SyncEngineTests
{
    private const string Owner = "bob@example.com";
    private const string Profile = "notes";
    private const string KeyA = "notes/a.md";
    private const string KeyB = "notes/b.md";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = Now.AddHours(-2);
    private static readonly DateTimeOffset Later = Now.AddHours(-1);

    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly Mock<ISyncEventLog> mockLog = new();
    private readonly List<SyncEvent> loggedEvents = [];
    private readonly Sha256ContentHasher hasher = new();
    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());
    private readonly InMemorySyncFileStore files;
    private readonly InMemorySyncIndexStore index = new();
    private readonly FakeSyncRemoteStore remote = new();
    private readonly SyncProfile profile;

    public SyncEngineTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
        mockLog.Setup(x => x.WriteAsync(It.IsAny<IEnumerable<SyncEvent>>()))
            .Callback<IEnumerable<SyncEvent>>(e => loggedEvents.AddRange(e))
            .Returns(Task.CompletedTask);
        files = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        profile = new SyncProfile(Profile, files, index, remote, new GlobSyncPathMatcher("notes/**/*.md", true));
    }

    public enum Scenario
    {
        NewLocal,
        ChangedLocal,
        NewRemote,
        ChangedRemote,
        SameOnBoth,
        DeletedLocally,
        DeletedRemotely,
        EditEdit,
        DeleteEdit,
        EditDelete,
    }

    public static TheoryData<Scenario, SyncEventKind> ScenarioEvents => new()
    {
        { Scenario.NewLocal, SyncEventKind.UploadNew },
        { Scenario.ChangedLocal, SyncEventKind.UploadLocalNewer },
        { Scenario.NewRemote, SyncEventKind.DownloadNew },
        { Scenario.ChangedRemote, SyncEventKind.DownloadRemoteNewer },
        { Scenario.SameOnBoth, SyncEventKind.AlreadyInSync },
        { Scenario.DeletedLocally, SyncEventKind.DeleteRemote },
        { Scenario.DeletedRemotely, SyncEventKind.DeleteLocal },
        { Scenario.EditEdit, SyncEventKind.ConflictResolved },
        { Scenario.DeleteEdit, SyncEventKind.ConflictResolved },
        { Scenario.EditDelete, SyncEventKind.ConflictResolved },
    };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var planner = new SyncPlanner();
        var resolver = new NewestWinsConflictResolver();

        // Act & Assert
        Assert.Equal("planner", Assert.Throws<ArgumentNullException>(() => new SyncEngine(null!, resolver, hasher, mockUser.Object, mockLog.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("conflictResolver", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, null!, hasher, mockUser.Object, mockLog.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("contentHasher", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, resolver, null!, mockUser.Object, mockLog.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, resolver, hasher, null!, mockLog.Object, mockClock.Object, format)).ParamName);
        Assert.Equal("eventLog", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, resolver, hasher, mockUser.Object, null!, mockClock.Object, format)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, resolver, hasher, mockUser.Object, mockLog.Object, null!, format)).ParamName);
        Assert.Equal("encryptedFileFormat", Assert.Throws<ArgumentNullException>(() => new SyncEngine(planner, resolver, hasher, mockUser.Object, mockLog.Object, mockClock.Object, null!)).ParamName);
    }

    [Theory]
    [MemberData(nameof(ScenarioEvents))]
    public async Task GivenDecisionTableRow_WhenSyncAsync_ThenWritesEventKindWithSharedPassId(Scenario scenario, SyncEventKind expected)
    {
        // Arrange
        await ArrangeAsync(scenario);

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(SyncEventKind.PassStarted, loggedEvents[0].Kind);
        Assert.Equal(SyncEventKind.PassCompleted, loggedEvents[^1].Kind);
        var fileEvent = Assert.Single(loggedEvents, e => e.Key == KeyA);
        Assert.Equal(expected, fileEvent.Kind);
        Assert.Single(loggedEvents.Select(e => e.PassId).Distinct());
        Assert.All(loggedEvents, e => Assert.Equal(Owner, e.Owner));
        Assert.All(loggedEvents, e => Assert.Equal(Profile, e.Profile));
    }

    [Fact]
    public async Task GivenNewLocalFile_WhenSyncAsync_ThenUploadedIfNoneMatchAndIndexRecordsBase()
    {
        // Arrange
        await TrackedWriteAsync(KeyA, "hello", Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Uploaded);
        Assert.Equal("hello", Text(remote.Objects[KeyA].Content));
        Assert.Equal(Later, remote.Objects[KeyA].ModifiedAt);
        var record = Record(KeyA);
        Assert.Equal(remote.Objects[KeyA].ETag, record.BaseETag);
        Assert.Equal(hasher.ComputeHash(Bytes("hello")), record.BaseHash);
        Assert.False(record.Dirty);
        Assert.Equal(Now, record.LastSyncedAt);
    }

    [Fact]
    public async Task GivenChangedLocalFile_WhenSyncAsync_ThenUploadsWithBaseETag()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedWriteAsync(KeyA, "v2", Later);
        var previousETag = remote.Objects[KeyA].ETag;

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Uploaded);
        Assert.Equal("v2", Text(remote.Objects[KeyA].Content));
        Assert.NotEqual(previousETag, Record(KeyA).BaseETag);
        Assert.False(Record(KeyA).Dirty);
    }

    [Fact]
    public async Task GivenChangedRemoteFile_WhenSyncAsync_ThenDownloadsAndReportsChangedKey()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Seed(KeyA, Bytes("v2"), Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Downloaded);
        var file = await files.ReadAsync(KeyA);
        Assert.Equal("v2", Text(file!.Content));
        Assert.Equal(Later, file.ModifiedAt);
        Assert.Equal([KeyA], result.ChangedLocalKeys[Profile]);
        Assert.Equal(remote.Objects[KeyA].ETag, Record(KeyA).BaseETag);
    }

    [Fact]
    public async Task GivenSameContentOnBothSidesWithNoIndex_WhenSyncAsync_ThenAdoptedWithoutTransfer()
    {
        // Arrange
        await files.WriteAsync(KeyA, Bytes("same"), Earlier);
        remote.Seed(KeyA, Bytes("same"), Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Adopted);
        Assert.Equal(0, result.Uploaded + result.Downloaded + result.ConflictsResolved);
        Assert.DoesNotContain(remote.Calls, c => c.StartsWith("put", StringComparison.Ordinal));
        Assert.Equal(remote.Objects[KeyA].ETag, Record(KeyA).BaseETag);
    }

    [Fact]
    public async Task GivenDifferentNewContentOnBothSides_WhenSyncAsync_ThenNewestWins()
    {
        // Arrange
        await TrackedWriteAsync(KeyA, "local", Later);
        remote.Seed(KeyA, Bytes("remote"), Earlier);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.ConflictsResolved);
        Assert.Equal("local", Text(remote.Objects[KeyA].Content));
        Assert.Contains(loggedEvents, e => e.Kind == SyncEventKind.ConflictResolved && e.Code == SyncEventCodes.ConflictKeptLocal);
    }

    [Fact]
    public async Task GivenEditOnBothSidesRemoteNewer_WhenSyncAsync_ThenRemoteVersionKept()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedWriteAsync(KeyA, "local", Earlier);
        remote.Seed(KeyA, Bytes("remote"), Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.ConflictsResolved);
        Assert.Equal("remote", Text((await files.ReadAsync(KeyA))!.Content));
        Assert.False(Record(KeyA).Dirty);
        Assert.Contains($"head {KeyA}", remote.Calls);
    }

    [Fact]
    public async Task GivenEditOnBothSidesLocalNewer_WhenSyncAsync_ThenLocalVersionUploadedOverCurrentETag()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Seed(KeyA, Bytes("remote"), Earlier);
        await TrackedWriteAsync(KeyA, "local", Later);

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal("local", Text(remote.Objects[KeyA].Content));
        Assert.Equal(remote.Objects[KeyA].ETag, Record(KeyA).BaseETag);
    }

    [Fact]
    public async Task GivenDeletedLocally_WhenSyncAsync_ThenRemoteDeletedAndRecordDropped()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedDeleteAsync(KeyA, Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.DeletedRemote);
        Assert.False(remote.Objects.ContainsKey(KeyA));
        Assert.Null(await index.GetAsync(Profile, Owner, KeyA));
    }

    [Fact]
    public async Task GivenDeletedLocallyButEditedRemotelyLater_WhenSyncAsync_ThenRemoteEditRestored()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedDeleteAsync(KeyA, Earlier);
        remote.Seed(KeyA, Bytes("edited"), Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.ConflictsResolved);
        Assert.Equal("edited", Text((await files.ReadAsync(KeyA))!.Content));
        Assert.False(Record(KeyA).Deleted);
    }

    [Fact]
    public async Task GivenDeletedLocallyAfterRemoteEdit_WhenSyncAsync_ThenRemoteDeleted()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Seed(KeyA, Bytes("edited"), Earlier);
        await TrackedDeleteAsync(KeyA, Later);

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.False(remote.Objects.ContainsKey(KeyA));
        Assert.Null(await index.GetAsync(Profile, Owner, KeyA));
    }

    [Fact]
    public async Task GivenDeletedRemotelyAndUnchangedLocally_WhenSyncAsync_ThenLocalCopyDeletedWithoutAsking()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Objects.Remove(KeyA);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.DeletedLocal);
        Assert.Null(await files.ReadAsync(KeyA));
        Assert.Null(await index.GetAsync(Profile, Owner, KeyA));
        Assert.Equal([KeyA], result.ChangedLocalKeys[Profile]);
        Assert.Contains(loggedEvents, e => e.Kind == SyncEventKind.DeleteLocal && e.Code == SyncEventCodes.DeletedRemotely);
    }

    [Fact]
    public async Task GivenDeletedRemotelyButEditedLocally_WhenSyncAsync_ThenLocalEditUploadedAgain()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Objects.Remove(KeyA);
        await TrackedWriteAsync(KeyA, "edited", Later);

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal("edited", Text(remote.Objects[KeyA].Content));
        Assert.False(Record(KeyA).Dirty);
    }

    [Fact]
    public async Task GivenRemoteChangedBetweenListAndUpload_WhenSyncAsync_ThenRetriedAndReplannedAsConflict()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedWriteAsync(KeyA, "local", Earlier);
        var changed = false;
        remote.BeforeWrite = key =>
        {
            if (!changed)
            {
                changed = true;
                remote.Seed(key, Bytes("other device"), Later);
            }
        };

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Contains(loggedEvents, e => e.Kind == SyncEventKind.Retried && e.Key == KeyA);
        Assert.Equal(1, result.ConflictsResolved);
        Assert.Equal("other device", Text((await files.ReadAsync(KeyA))!.Content));
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task GivenRemoteChangedBetweenListAndDelete_WhenSyncAsync_ThenRetriedAndNewerEditKept()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await TrackedDeleteAsync(KeyA, Earlier);
        var changed = false;
        remote.BeforeWrite = key =>
        {
            if (!changed)
            {
                changed = true;
                remote.Seed(key, Bytes("other device"), Later);
            }
        };

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Contains(loggedEvents, e => e.Kind == SyncEventKind.Retried && e.Key == KeyA);
        Assert.Equal("other device", Text((await files.ReadAsync(KeyA))!.Content));
        Assert.True(remote.Objects.ContainsKey(KeyA));
    }

    [Fact]
    public async Task GivenOneFileFails_WhenSyncAsync_ThenFailureRecordedAndOtherFilesStillSync()
    {
        // Arrange
        remote.Seed(KeyA, Bytes("a"), Earlier);
        remote.Seed(KeyB, Bytes("b"), Earlier);
        remote.FailGet[KeyB] = new InvalidOperationException("boom");

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, result.Failed);
        Assert.NotNull(await files.ReadAsync(KeyA));
        Assert.NotNull(await index.GetAsync(Profile, Owner, KeyA));
        var failed = Assert.Single(loggedEvents, e => e.Kind == SyncEventKind.Failed);
        Assert.Equal(KeyB, failed.Key);
        Assert.Equal(SyncEventCodes.DownloadFailed, failed.Code);
        Assert.Empty(failed.Arguments);
        Assert.Contains("boom", failed.Detail, StringComparison.Ordinal);
        Assert.Equal(SyncEventCodes.Summary, loggedEvents[^1].Code);
        Assert.Equal(["0", "1", "0", "0", "1"], loggedEvents[^1].Arguments);
    }

    [Fact]
    public async Task GivenFilesInKeyOrder_WhenSyncAsync_ThenActionsRunInKeyOrderWithIndexUpdatedAfterEach()
    {
        // Arrange
        await TrackedWriteAsync(KeyB, "b", Later);
        await TrackedWriteAsync(KeyA, "a", Later);
        SyncIndexRecord? recordAWhenBUploaded = null;
        remote.BeforeWrite = key =>
        {
            if (key == KeyB)
            {
                recordAWhenBUploaded = index.GetAsync(Profile, Owner, KeyA).Result;
            }
        };

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(["list", $"put {KeyA}", $"put {KeyB}"], remote.Calls);
        Assert.NotNull(recordAWhenBUploaded?.BaseETag);
    }

    [Fact]
    public async Task GivenRemoteUnreachable_WhenSyncAsync_ThenThrowsAndLogsFailure()
    {
        // Arrange
        remote.FailList = new SyncNetworkException(SyncErrorCodes.RemoteUnreachable);

        // Act & Assert
        await Assert.ThrowsAsync<SyncNetworkException>(() => CreateSut().SyncAsync(profile, false));
        Assert.Contains(loggedEvents, e => e.Kind == SyncEventKind.Failed && e.Code == SyncEventCodes.PassFailed && e.Arguments.SequenceEqual([SyncErrorCodes.RemoteUnreachable]));
    }

    [Fact]
    public async Task GivenNothingChanged_WhenSyncAsync_ThenRecordsCollapsibleNoChangePass()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.False(result.HasChanges);
        mockLog.Verify(x => x.RecordNoChangePassAsync(Owner, Profile, It.IsAny<string>(), Now), Times.Once);
        Assert.Empty(loggedEvents);
    }

    [Fact]
    public async Task GivenFileWrittenWithoutTracking_WhenVerifyPass_ThenHashDifferenceIsUploaded()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        await files.WriteAsync(KeyA, Bytes("crashed before the index was marked"), Later);

        // Act
        var normal = await CreateSut().SyncAsync(profile, false);
        var verify = await CreateSut().SyncAsync(profile, true);

        // Assert
        Assert.Equal(0, normal.Uploaded);
        Assert.Equal(1, verify.Uploaded);
        Assert.Equal("crashed before the index was marked", Text(remote.Objects[KeyA].Content));
    }

    [Fact]
    public async Task GivenLocalEditDuringUpload_WhenSyncAsync_ThenFileStaysDirty()
    {
        // Arrange
        await TrackedWriteAsync(KeyA, "v1", Earlier);
        remote.BeforeWrite = _ => TrackedWriteAsync(KeyA, "v2", Later).GetAwaiter().GetResult();

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.True(Record(KeyA).Dirty);
        Assert.Equal("v1", Text(remote.Objects[KeyA].Content));
    }

    [Fact]
    public async Task GivenLocalEditAfterSnapshot_WhenDownloading_ThenLocalEditIsNotOverwritten()
    {
        // Arrange
        await SyncedAsync(KeyA, "v1");
        remote.Seed(KeyA, Bytes("remote"), Later);
        var mockPlanner = new Mock<ISyncPlanner>();
        mockPlanner.Setup(x => x.Plan(It.IsAny<IEnumerable<SyncFileInfo>>(), It.IsAny<IEnumerable<RemoteObjectInfo>>(), It.IsAny<IEnumerable<SyncIndexRecord>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Returns<IEnumerable<SyncFileInfo>, IEnumerable<RemoteObjectInfo>, IEnumerable<SyncIndexRecord>, IReadOnlyDictionary<string, string>?>((l, r, i, h) =>
            {
                var plan = new SyncPlanner().Plan(l, r, i, h);
                TrackedWriteAsync(KeyA, "typed during the pass", Now).GetAwaiter().GetResult();
                return plan;
            });
        var sut = new SyncEngine(mockPlanner.Object, new NewestWinsConflictResolver(), hasher, mockUser.Object, mockLog.Object, mockClock.Object, format);

        // Act
        var result = await sut.SyncAsync(profile, false);

        // Assert
        Assert.Equal(0, result.Downloaded);
        Assert.Equal("typed during the pass", Text((await files.ReadAsync(KeyA))!.Content));
        Assert.True(Record(KeyA).Dirty);
    }

    [Fact]
    public async Task GivenFilesOutsideThePattern_WhenSyncAsync_ThenTheyAreIgnored()
    {
        // Arrange
        await TrackedWriteAsync("notes/image.png", "png", Later);
        await TrackedWriteAsync("other/a.md", "other", Later);

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(0, result.Uploaded);
        Assert.Empty(remote.Objects);
    }

    [Fact]
    public async Task GivenEmptyFolderMarker_WhenSyncAsync_ThenUploadedAsZeroByteKey()
    {
        // Arrange
        await files.CreateFolderAsync("notes/Empty/");

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Empty(remote.Objects["notes/Empty/"].Content);
    }

    [Fact]
    public async Task GivenSignedOut_WhenSyncAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SyncAsync(profile, false));
    }

    [Fact]
    public async Task GivenLogFails_WhenSyncAsync_ThenPassStillSucceeds()
    {
        // Arrange
        await TrackedWriteAsync(KeyA, "a", Later);
        mockLog.Setup(x => x.WriteAsync(It.IsAny<IEnumerable<SyncEvent>>())).ThrowsAsync(new InvalidOperationException("no log"));

        // Act
        var result = await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(1, result.Uploaded);
    }

    [Fact]
    public async Task GivenEncryptedRemoteFile_WhenSyncAsync_ThenDownloadedAsIsAndIndexRecordsItsKey()
    {
        // Arrange
        var encrypted = format.Encrypt(Bytes("secret"), "k_1", new byte[32]);
        remote.Seed(KeyA, encrypted, Later);

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(encrypted, (await files.ReadAsync(KeyA))!.Content);
        var record = Record(KeyA);
        Assert.True(record.Encrypted);
        Assert.Equal("k_1", record.KeyId);
    }

    [Fact]
    public async Task GivenEncryptedLocalFile_WhenSyncAsync_ThenUploadedAsIsAndIndexKeepsItsKey()
    {
        // Arrange
        var encrypted = format.Encrypt(Bytes("secret"), "k_1", new byte[32]);
        await files.WriteAsync(KeyA, encrypted, Later);
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = KeyA, Dirty = true, Revision = 1 });

        // Act
        await CreateSut().SyncAsync(profile, false);

        // Assert
        Assert.Equal(encrypted, remote.Objects[KeyA].Content);
        Assert.Equal("k_1", Record(KeyA).KeyId);
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);

    private SyncEngine CreateSut() =>
        new(new SyncPlanner(), new NewestWinsConflictResolver(), hasher, mockUser.Object, mockLog.Object, mockClock.Object, format);

    private SyncIndexRecord Record(string key) => index.GetAsync(Profile, Owner, key).Result!;

    private async Task SyncedAsync(string key, string content)
    {
        await files.WriteAsync(key, Bytes(content), Earlier.AddDays(-1));
        var etag = remote.Seed(key, Bytes(content), Earlier.AddDays(-1));
        await index.SaveAsync(new SyncIndexRecord
        {
            Profile = Profile,
            Owner = Owner,
            Key = key,
            BaseETag = etag,
            BaseHash = hasher.ComputeHash(Bytes(content)),
            Revision = 1,
        });
    }

    private async Task TrackedWriteAsync(string key, string content, DateTimeOffset modifiedAt)
    {
        await files.WriteAsync(key, Bytes(content), modifiedAt);
        var record = await index.GetAsync(Profile, Owner, key) ?? new SyncIndexRecord { Profile = Profile, Owner = Owner, Key = key };
        record.Revision++;
        record.Dirty = true;
        record.Deleted = false;
        await index.SaveAsync(record);
    }

    private async Task TrackedDeleteAsync(string key, DateTimeOffset deletedAt)
    {
        await files.DeleteAsync(key);
        var record = (await index.GetAsync(Profile, Owner, key))!;
        record.Revision++;
        record.Dirty = false;
        record.Deleted = true;
        record.DeletedAt = deletedAt;
        await index.SaveAsync(record);
    }

    private async Task ArrangeAsync(Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.NewLocal:
                await TrackedWriteAsync(KeyA, "new", Later);
                break;
            case Scenario.ChangedLocal:
                await SyncedAsync(KeyA, "v1");
                await TrackedWriteAsync(KeyA, "v2", Later);
                break;
            case Scenario.NewRemote:
                remote.Seed(KeyA, Bytes("new"), Later);
                break;
            case Scenario.ChangedRemote:
                await SyncedAsync(KeyA, "v1");
                remote.Seed(KeyA, Bytes("v2"), Later);
                break;
            case Scenario.SameOnBoth:
                await TrackedWriteAsync(KeyA, "same", Earlier);
                remote.Seed(KeyA, Bytes("same"), Later);
                break;
            case Scenario.DeletedLocally:
                await SyncedAsync(KeyA, "v1");
                await TrackedDeleteAsync(KeyA, Later);
                break;
            case Scenario.DeletedRemotely:
                await SyncedAsync(KeyA, "v1");
                remote.Objects.Remove(KeyA);
                break;
            case Scenario.EditEdit:
                await SyncedAsync(KeyA, "v1");
                await TrackedWriteAsync(KeyA, "local", Later);
                remote.Seed(KeyA, Bytes("remote"), Earlier);
                break;
            case Scenario.DeleteEdit:
                await SyncedAsync(KeyA, "v1");
                await TrackedDeleteAsync(KeyA, Later);
                remote.Seed(KeyA, Bytes("remote"), Earlier);
                break;
            case Scenario.EditDelete:
                await SyncedAsync(KeyA, "v1");
                await TrackedWriteAsync(KeyA, "local", Later);
                remote.Objects.Remove(KeyA);
                break;
        }
    }
}
