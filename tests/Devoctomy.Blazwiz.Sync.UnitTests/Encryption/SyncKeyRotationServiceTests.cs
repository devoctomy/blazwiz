using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class SyncKeyRotationServiceTests
{
    private const string Owner = "bob";
    private const string OldKeyId = "k_old";
    private const string NewKeyId = "k_new";

    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));
    private readonly BouncyCastleAesGcmAeadCipher cipher = new();
    private readonly EncryptedFileFormat format;
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
    private readonly E2eKeyRing ring;
    private readonly byte[] oldKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] newKey = RandomNumberGenerator.GetBytes(32);
    private readonly InMemorySyncFileStore files;
    private readonly InMemorySyncIndexStore index = new();
    private readonly EncryptingSyncFileStore store;
    private readonly FakeSyncProfileRegistry registry;
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<IE2eKeyRings> mockRings = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncLockService> mockLock = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly Mock<ISyncScratchStore> mockScratch = new();
    private readonly List<string> calls = [];

    public SyncKeyRotationServiceTests()
    {
        format = new EncryptedFileFormat(cipher);
        ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), scope);
        var user = new Mock<ISyncUserContext>();
        user.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        files = new InMemorySyncFileStore(user.Object, clock);
        var tracking = new TrackingSyncFileStore(scope.Id, files, index, user.Object, new Mock<ISyncScheduler>().Object, clock, format);
        store = new EncryptingSyncFileStore(tracking, ring, format);
        var options = new SyncOptions();
        options.Profiles.Add(new SyncProfileOptions { Name = scope.Id, KeyScope = scope, RemotePattern = "profiles/**/*" });
        registry = FakeSyncProfileRegistry.Of(options);
        registry.SetFileStores(scope.Id, store, files);

        mockRings.Setup(x => x.Get(scope)).Returns(ring);
        mockTargets.SetupGet(x => x.IsConfigured).Returns(true);
        mockConnectivity.Setup(x => x.RecheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConnectivityStatus { IsOnline = true });
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add("lock " + name))
            .ReturnsAsync(new Mock<IAsyncDisposable>().Object);
        mockScratch.Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((name, _, _) =>
            {
                calls.Add("put " + name);
                clock.Advance(TimeSpan.FromSeconds(2));
            })
            .Returns(Task.CompletedTask);
        mockScratch.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add("delete " + name))
            .Returns(Task.CompletedTask);
        mockSync.Setup(x => x.SyncAsync(scope.Id, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns<string?, bool, IProgress<SyncPassProgress>?, CancellationToken>((_, _, progress, _) => PushAsync(progress));
        mockProfiles.Setup(x => x.ChangePassphraseAsync(scope, "old", "new", It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { [OldKeyId] = oldKey, [NewKeyId] = newKey }, NewKeyId))
            .Returns(Task.CompletedTask);
        ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { [OldKeyId] = oldKey }, OldKeyId);
    }

    [Fact]
    public async Task GivenAProfile_WhenPrepared_ThenItIsSyncedFirstAndAThrowawayFileIsTimedAndDeleted()
    {
        // Arrange
        await store.WriteAsync("profiles/a.md", new byte[1000]);
        await store.WriteAsync("profiles/b.md", new byte[3000]);
        await store.CreateFolderAsync("profiles/Work/");
        var sut = CreateSut();

        // Act
        var estimate = await sut.PrepareAsync(scope);

        // Assert
        Assert.Equal(2, estimate.FileCount);
        Assert.Equal(TimeSpan.FromSeconds(4), estimate.Duration);
        Assert.True(estimate.TotalBytes > 4000);
        Assert.Equal("lock " + SyncDefaults.LockNamePrefix + scope.Id, calls[0]);
        Assert.Equal("sync", calls[1]);
        Assert.StartsWith("put ", calls[2]);
        Assert.Equal("delete " + calls[2]["put ".Length..], calls[3]);
        Assert.Equal(4, calls.Count);
        Assert.All((await store.ListAsync()).Where(f => !f.IsFolder), f => Assert.Equal(OldKeyId, f.EncryptionKeyId));
    }

    [Fact]
    public async Task GivenOffline_WhenPrepared_ThenRefusedWithNothingSyncedOrUploaded()
    {
        // Arrange
        mockConnectivity.Setup(x => x.RecheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.NoNetwork });
        var sut = CreateSut();

        // Act
        var ex = await Assert.ThrowsAsync<SyncKeyRotationException>(() => sut.PrepareAsync(scope));

        // Assert
        Assert.Equal(SyncErrorCodes.RotationOffline, ex.Code);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenTheSyncFails_WhenPrepared_ThenRefused()
    {
        // Arrange
        mockSync.Setup(x => x.SyncAsync(scope.Id, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult { Failed = 1 });
        var sut = CreateSut();

        // Act
        var ex = await Assert.ThrowsAsync<SyncKeyRotationException>(() => sut.PrepareAsync(scope));

        // Assert
        Assert.Equal(SyncErrorCodes.RotationSyncFailed, ex.Code);
        mockScratch.Verify(x => x.PutAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheThrowawayUploadCannotReachS3_WhenPrepared_ThenRefusedAsOfflineAndTheFileIsStillDeleted()
    {
        // Arrange
        await store.WriteAsync("profiles/a.md", new byte[10]);
        mockScratch.Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable, new HttpRequestException()));
        var sut = CreateSut();

        // Act
        var ex = await Assert.ThrowsAsync<SyncKeyRotationException>(() => sut.PrepareAsync(scope));

        // Assert
        Assert.Equal(SyncErrorCodes.RotationOffline, ex.Code);
        mockScratch.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoSyncTarget_WhenPrepared_ThenNothingIsSyncedOrUploaded()
    {
        // Arrange: local development.
        mockTargets.SetupGet(x => x.IsConfigured).Returns(false);
        await store.WriteAsync("profiles/a.md", new byte[10]);
        var sut = CreateSut();

        // Act
        var estimate = await sut.PrepareAsync(scope);

        // Assert
        Assert.Equal(1, estimate.FileCount);
        Assert.Equal(TimeSpan.Zero, estimate.Duration);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenFilesUnderTheOldKey_WhenRotated_ThenEveryFileIsEncryptedAgainUnderTheNewKeyThenUploadedOnce()
    {
        // Arrange
        var edited = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await store.WriteAsync("profiles/a.md", Bytes("alpha"), edited);
        await store.WriteAsync("profiles/b.md", Bytes("bravo"));
        await store.CreateFolderAsync("profiles/Work/");
        await PushAsync(null);
        calls.Clear();
        var reported = new List<E2eProgress>();
        var sut = CreateSut();

        // Act
        await sut.RotateAsync(scope, "old", "new", new Reporter(reported));

        // Assert
        Assert.Equal(["profiles/a.md", "profiles/b.md"], (await files.ListAsync()).Where(f => !f.IsFolder).Select(f => f.Key).Order());
        foreach (var (key, text) in new[] { ("profiles/a.md", "alpha"), ("profiles/b.md", "bravo") })
        {
            var raw = await files.ReadAsync(key);
            Assert.True(format.TryGetKeyId(raw!.Content, out var keyId));
            Assert.Equal(NewKeyId, keyId);
            Assert.Equal(text, Encoding.UTF8.GetString((await store.ReadAsync(key))!.Content));
        }

        Assert.Equal(edited, (await files.ReadAsync("profiles/a.md"))!.ModifiedAt);
        Assert.Equal(["lock " + SyncDefaults.LockNamePrefix + scope.Id, "sync"], calls);
        Assert.DoesNotContain(index.Records, r => r.Dirty);
        Assert.Equal([scope], sut.ReadyToComplete);
        var last = reported.Where(p => p.Step == E2eStep.ReencryptFiles).Last();
        Assert.True(last.Completed);
        Assert.Equal((2, 2), (last.Done, last.Total));
        Assert.Equal(
            [(0, 2), (1, 2), (2, 2)],
            reported.Where(p => p.Step == E2eStep.UploadFiles).Select(p => (p.Done, p.Total)).Distinct());
        Assert.True(reported.Last(p => p.Step == E2eStep.UploadFiles).Completed);
    }

    [Fact]
    public async Task GivenSomeFilesAlreadyUnderTheNewKey_WhenAnInterruptedRotationIsFinished_ThenItSyncsFirstAndOnlyTheOthersAreEncryptedAgain()
    {
        // Arrange: cut short part way through.
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        await store.WriteAsync("profiles/b.md", Bytes("bravo"));
        ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { [OldKeyId] = oldKey, [NewKeyId] = newKey }, NewKeyId);
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        await PushAsync(null);
        var revisionOfA = (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!.Revision;
        calls.Clear();
        var sut = CreateSut();

        // Act
        await sut.FinishAsync(scope);

        // Assert
        Assert.Equal(revisionOfA, (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!.Revision);
        Assert.Equal(NewKeyId, (await index.GetAsync(scope.Id, Owner, "profiles/b.md"))!.KeyId);
        Assert.Equal(["lock " + SyncDefaults.LockNamePrefix + scope.Id, "sync", "lock " + SyncDefaults.LockNamePrefix + scope.Id, "sync"], calls);
        Assert.Equal([scope], sut.ReadyToComplete);
    }

    [Fact]
    public async Task GivenEveryFileEncryptedAgainAndOnlyTheSignOutLeft_WhenFinished_ThenNothingIsEncryptedAgainAndItIsReadyToComplete()
    {
        // Arrange: cut short after the upload, before every device was signed out.
        ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { [OldKeyId] = oldKey, [NewKeyId] = newKey }, NewKeyId);
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        await PushAsync(null);
        var revision = (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!.Revision;
        var reported = new List<E2eProgress>();
        calls.Clear();
        var sut = CreateSut();

        // Act
        await sut.FinishAsync(scope, new Reporter(reported));
        await sut.CompleteAsync();

        // Assert
        Assert.Equal(revision, (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!.Revision);
        Assert.Equal(["lock " + SyncDefaults.LockNamePrefix + scope.Id, "sync"], calls);
        Assert.All(reported, p => Assert.True(p.Completed));
        mockManager.Verify(x => x.CompleteRotationAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnotherDevicesEditWinsAConflict_WhenRotated_ThenTheFileIsEncryptedAgainInAnotherRound()
    {
        // Arrange: the first upload brings back their version, still under the old key.
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        await PushAsync(null);
        var passes = 0;
        mockSync.Setup(x => x.SyncAsync(scope.Id, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns<string?, bool, IProgress<SyncPassProgress>?, CancellationToken>(async (_, _, progress, _) =>
            {
                if (++passes == 1)
                {
                    await files.WriteAsync("profiles/a.md", format.Encrypt(Bytes("theirs"), OldKeyId, oldKey, new E2eFileBinding(scope.Id, "profiles/a.md")));
                    var record = (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!;
                    record.KeyId = OldKeyId;
                    record.Dirty = false;
                    await index.SaveAsync(record);
                    return new SyncResult { ConflictsResolved = 1 };
                }

                return await PushAsync(progress);
            });
        var sut = CreateSut();

        // Act
        await sut.RotateAsync(scope, "old", "new");

        // Assert
        Assert.Equal(2, passes);
        Assert.Equal("theirs", Encoding.UTF8.GetString((await store.ReadAsync("profiles/a.md"))!.Content));
        Assert.Equal(NewKeyId, (await index.GetAsync(scope.Id, Owner, "profiles/a.md"))!.KeyId);
    }

    [Fact]
    public async Task GivenTheUploadFails_WhenRotated_ThenItThrowsAndTheRotationIsNotReadyToComplete()
    {
        // Arrange
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        mockSync.Setup(x => x.SyncAsync(scope.Id, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult { Errors = { "unreachable" } });
        var sut = CreateSut();

        // Act
        var ex = await Assert.ThrowsAsync<SyncKeyRotationException>(() => sut.RotateAsync(scope, "old", "new"));

        // Assert
        Assert.Equal(SyncErrorCodes.RotationSyncFailed, ex.Code);
        Assert.Empty(sut.ReadyToComplete);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenFinished_ThenThrowsLocked()
    {
        // Arrange
        ring.Clear();
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<E2eLockedException>(() => sut.FinishAsync(scope));
    }

    [Fact]
    public async Task GivenAFinishedRotation_WhenCompleted_ThenItsMarkIsClearedOnce()
    {
        // Arrange
        await store.WriteAsync("profiles/a.md", Bytes("alpha"));
        var sut = CreateSut();
        await sut.RotateAsync(scope, "old", "new");

        // Act
        await sut.CompleteAsync();
        await sut.CompleteAsync();

        // Assert
        mockManager.Verify(x => x.CompleteRotationAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(sut.ReadyToComplete);
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenPrepared_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.PrepareAsync(new E2eKeyScope(Guid.NewGuid().ToString("D"))));
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    // What a pass does with this device's changes: uploads every dirty file, so it is no longer dirty.
    private async Task<SyncResult> PushAsync(IProgress<SyncPassProgress>? progress)
    {
        calls.Add("sync");
        var dirty = (await index.GetAllAsync(scope.Id, Owner)).Where(r => r.Dirty).ToList();
        progress?.Report(new SyncPassProgress(0, dirty.Count));
        for (var i = 0; i < dirty.Count; i++)
        {
            dirty[i].Dirty = false;
            dirty[i].BaseETag = "etag";
            await index.SaveAsync(dirty[i]);
            progress?.Report(new SyncPassProgress(i + 1, dirty.Count));
        }

        return new SyncResult { Uploaded = dirty.Count };
    }

    private SyncKeyRotationService CreateSut() => new(
        mockProfiles.Object,
        mockManager.Object,
        mockRings.Object,
        registry,
        mockSync.Object,
        mockConnectivity.Object,
        mockLock.Object,
        mockTargets.Object,
        mockScratch.Object,
        format,
        cipher,
        clock);

    private sealed class Reporter(List<E2eProgress> reported) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => reported.Add(value);
    }
}
