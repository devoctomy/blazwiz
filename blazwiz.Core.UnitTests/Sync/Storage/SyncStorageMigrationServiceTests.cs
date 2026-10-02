using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.Storage;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Storage;

public class SyncStorageMigrationServiceTests
{
    private const string Owner = "bob";
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISystemClock> mockClock = new();
    private readonly Mock<IOpfsClient> mockOpfs = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<ISyncLockService> mockLock = new();
    private readonly Mock<ISyncStorageChannel> mockChannel = new();
    private readonly FakeStateStore stateStore = new();
    private readonly SyncOptions options = new();
    private readonly List<string> calls = [];
    private readonly InMemorySyncFileStore indexedDb;
    private ISyncFileStore opfs;

    public SyncStorageMigrationServiceTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UnixEpoch);
        mockScheduler.SetupGet(x => x.IsStarted).Returns(true);
        mockScheduler.Setup(x => x.StopAsync()).Callback(() => calls.Add("stop")).Returns(Task.CompletedTask);
        mockScheduler.Setup(x => x.StartAsync()).Callback(() => calls.Add("start")).Returns(Task.CompletedTask);
        mockChannel.Setup(x => x.PostAsync(It.IsAny<SyncStorageChannelMessage>())).Callback<SyncStorageChannelMessage>(m => calls.Add(m.ToString())).Returns(Task.CompletedTask);
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add("lock " + name))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        indexedDb = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        opfs = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        var notes = new SyncProfileOptions { Name = "notes" };
        notes.FileStores.Add(new SyncStorageOptions(SyncStorageKind.IndexedDb, _ => indexedDb));
        notes.FileStores.Add(new SyncStorageOptions(SyncStorageKind.Opfs, _ => opfs));
        options.Profiles.Add(notes);
        options.Profiles.Add(new SyncProfileOptions { Name = "photos" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var resolver = CreateResolver();
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(null!, resolver, stateStore, mockScheduler.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("resolver", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(options, null!, stateStore, mockScheduler.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("stateStore", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(options, resolver, null!, mockScheduler.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(options, resolver, stateStore, null!, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(options, resolver, stateStore, mockScheduler.Object, null!, mockChannel.Object)).ParamName);
        Assert.Equal("channel", Assert.Throws<ArgumentNullException>(() => new SyncStorageMigrationService(options, resolver, stateStore, mockScheduler.Object, mockLock.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenFilesInIndexedDb_WhenMigrateToOpfs_ThenMovedWithTimesUnderTheLockWithSyncPaused()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1, 2], Earlier);
        await indexedDb.WriteAsync("notes/Work/", [], Earlier);
        var progress = new List<(int Done, int Total)>();

        // Act
        await CreateSut().MigrateAsync(SyncStorageKind.Opfs, new Reporter(p => progress.Add((p.Done, p.Total))));

        // Assert
        Assert.Empty(await indexedDb.ListAsync());
        var a = await opfs.ReadAsync("notes/a.md");
        Assert.Equal([1, 2], a!.Content);
        Assert.Equal(Earlier, a.ModifiedAt);
        Assert.NotNull(await opfs.ReadAsync("notes/Work/"));
        Assert.Equal([(0, 2), (1, 2), (2, 2)], progress);
        Assert.Equal(["stop", "Migrating", "lock blazwiz-sync-notes", "Changed", "start"], calls);

        var state = stateStore.Saved.Last();
        Assert.Equal(("notes", Owner, SyncStorageKind.Opfs), (state.Profile, state.Owner, state.Kind));
        Assert.Null(state.MigrationPhase);
        Assert.Equal(
            [SyncStorageMigrationPhase.Copying, SyncStorageMigrationPhase.Cleanup, null],
            stateStore.Saved.Select(s => s.MigrationPhase));
        Assert.Equal(SyncStorageKind.IndexedDb, stateStore.Saved[0].Kind);
        Assert.Equal((SyncStorageKind.IndexedDb, SyncStorageKind.Opfs), (stateStore.Saved[0].MigrationFrom, stateStore.Saved[0].MigrationTo));
        Assert.Equal(SyncStorageKind.Opfs, stateStore.Saved[1].Kind);
    }

    [Fact]
    public async Task GivenLeftoverFilesInTarget_WhenMigrate_ThenClearedBeforeCopying()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1]);
        await opfs.WriteAsync("notes/stale.md", [9]);

        // Act
        await CreateSut().MigrateAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.Equal(["notes/a.md"], (await opfs.ListAsync()).Select(f => f.Key));
    }

    [Fact]
    public async Task GivenAlreadyOnTarget_WhenMigrate_ThenNothingHappens()
    {
        // Act
        await CreateSut().MigrateAsync(SyncStorageKind.IndexedDb);

        // Assert
        Assert.Empty(calls);
        Assert.Empty(stateStore.Saved);
    }

    [Fact]
    public async Task GivenSyncNotStarted_WhenMigrate_ThenNotStartedAfterwards()
    {
        // Arrange
        mockScheduler.SetupGet(x => x.IsStarted).Returns(false);

        // Act
        await CreateSut().MigrateAsync(SyncStorageKind.Opfs);

        // Assert
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSyncFailsToStart_WhenMigrate_ThenMoveStillSucceeds()
    {
        // Arrange
        mockScheduler.Setup(x => x.StartAsync()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        await CreateSut().MigrateAsync(SyncStorageKind.Opfs);

        // Assert
        Assert.Equal(SyncStorageKind.Opfs, stateStore.Saved.Last().Kind);
    }

    [Fact]
    public async Task GivenCopyDoesNotMatch_WhenMigrate_ThenUndoneAndSourceKeptInUse()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1, 2]);
        opfs = new CorruptingStore(new InMemorySyncFileStore(mockUser.Object, mockClock.Object));

        // Act
        var ex = await Assert.ThrowsAsync<SyncStorageMigrationException>(() => CreateSut().MigrateAsync(SyncStorageKind.Opfs));

        // Assert
        Assert.Equal("'notes/a.md' did not copy correctly.", ex.Message);
        Assert.Empty(await opfs.ListAsync());
        Assert.Single(await indexedDb.ListAsync());
        var state = stateStore.Saved.Last();
        Assert.Equal(SyncStorageKind.IndexedDb, state.Kind);
        Assert.Null(state.MigrationPhase);
        Assert.Equal(["stop", "Migrating", "lock blazwiz-sync-notes", "Changed", "start"], calls);
    }

    [Fact]
    public async Task GivenUndoFails_WhenMigrateFails_ThenOriginalFailureReported()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1, 2]);
        var corrupting = new CorruptingStore(new InMemorySyncFileStore(mockUser.Object, mockClock.Object)) { FailDeletes = true };
        opfs = corrupting;

        // Act
        var ex = await Assert.ThrowsAsync<SyncStorageMigrationException>(() => CreateSut().MigrateAsync(SyncStorageKind.Opfs));

        // Assert
        Assert.Equal("'notes/a.md' did not copy correctly.", ex.Message);
        Assert.Equal(SyncStorageKind.IndexedDb, stateStore.Saved.Last().Kind);
    }

    [Fact]
    public async Task GivenSourceFileVanishes_WhenMigrate_ThenFails()
    {
        // Arrange
        var source = new Mock<ISyncFileStore>();
        source.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new SyncFileInfo { Key = "notes/a.md" }]);
        options.Profiles[0].FileStores[0] = new SyncStorageOptions(SyncStorageKind.IndexedDb, _ => source.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SyncStorageMigrationException>(() => CreateSut().MigrateAsync(SyncStorageKind.Opfs));
        Assert.Equal("'notes/a.md' could not be read.", ex.Message);
    }

    [Fact]
    public async Task GivenCancelled_WhenMigrate_ThenUndone()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1]);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().MigrateAsync(SyncStorageKind.Opfs, null, new CancellationToken(canceled: true)));

        // Assert
        Assert.Equal(SyncStorageKind.IndexedDb, stateStore.Saved.Last().Kind);
        Assert.Single(await indexedDb.ListAsync());
    }

    [Fact]
    public async Task GivenCopyCutShort_WhenRecoverAsync_ThenTargetClearedAndSourceKept()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1]);
        await opfs.WriteAsync("notes/a.md", [1]);
        stateStore.Set(new SyncStorageState
        {
            Profile = "notes", Owner = Owner, Kind = SyncStorageKind.IndexedDb,
            MigrationFrom = SyncStorageKind.IndexedDb, MigrationTo = SyncStorageKind.Opfs, MigrationPhase = SyncStorageMigrationPhase.Copying,
        });

        // Act
        await CreateSut().RecoverAsync();

        // Assert
        Assert.Empty(await opfs.ListAsync());
        Assert.Single(await indexedDb.ListAsync());
        var state = stateStore.Saved.Last();
        Assert.Equal(SyncStorageKind.IndexedDb, state.Kind);
        Assert.Null(state.MigrationPhase);
        Assert.Equal(["lock blazwiz-sync-notes"], calls);
    }

    [Fact]
    public async Task GivenCleanupCutShort_WhenRecoverAsync_ThenSourceClearedAndTargetKept()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1]);
        await opfs.WriteAsync("notes/a.md", [1]);
        stateStore.Set(new SyncStorageState
        {
            Profile = "notes", Owner = Owner, Kind = SyncStorageKind.Opfs,
            MigrationFrom = SyncStorageKind.IndexedDb, MigrationTo = SyncStorageKind.Opfs, MigrationPhase = SyncStorageMigrationPhase.Cleanup,
        });

        // Act
        await CreateSut().RecoverAsync();

        // Assert
        Assert.Empty(await indexedDb.ListAsync());
        Assert.Single(await opfs.ListAsync());
        var state = stateStore.Saved.Last();
        Assert.Equal(SyncStorageKind.Opfs, state.Kind);
        Assert.Null(state.MigrationPhase);
    }

    [Fact]
    public async Task GivenIncompleteRecordWithoutFromOrTo_WhenRecoverAsync_ThenOnlyStateReset()
    {
        // Arrange
        await indexedDb.WriteAsync("notes/a.md", [1]);
        stateStore.Set(new SyncStorageState { Profile = "notes", Owner = Owner, Kind = SyncStorageKind.IndexedDb, MigrationPhase = SyncStorageMigrationPhase.Copying });
        var sut = CreateSut();

        // Act
        await sut.RecoverAsync();
        stateStore.Set(new SyncStorageState { Profile = "notes", Owner = Owner, Kind = SyncStorageKind.IndexedDb, MigrationPhase = SyncStorageMigrationPhase.Cleanup });
        await sut.RecoverAsync();

        // Assert
        Assert.Single(await indexedDb.ListAsync());
        Assert.Null(stateStore.Saved.Last().MigrationPhase);
    }

    [Fact]
    public async Task GivenAnotherTabFinishedWhileWaitingForTheLock_WhenRecoverAsync_ThenNothingChanged()
    {
        // Arrange
        await opfs.WriteAsync("notes/a.md", [1]);
        stateStore.Set(new SyncStorageState
        {
            Profile = "notes", Owner = Owner, Kind = SyncStorageKind.IndexedDb,
            MigrationFrom = SyncStorageKind.IndexedDb, MigrationTo = SyncStorageKind.Opfs, MigrationPhase = SyncStorageMigrationPhase.Copying,
        });
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => stateStore.Set(new SyncStorageState { Profile = "notes", Owner = Owner, Kind = SyncStorageKind.Opfs }))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        // Act
        await CreateSut().RecoverAsync();

        // Assert
        Assert.Single(await opfs.ListAsync());
        Assert.Empty(stateStore.Saved);
    }

    [Fact]
    public async Task GivenNoMoveInProgress_WhenRecoverAsync_ThenNoLockTaken()
    {
        // Act
        await CreateSut().RecoverAsync();

        // Assert
        Assert.Empty(calls);
    }

    private SyncStorageResolver CreateResolver() => new(Mock.Of<IServiceProvider>(), options, stateStore, mockUser.Object, mockOpfs.Object);

    private SyncStorageMigrationService CreateSut() => new(options, CreateResolver(), stateStore, mockScheduler.Object, mockLock.Object, mockChannel.Object);

    private sealed class FakeStateStore : ISyncStorageStateStore
    {
        private SyncStorageState? current;

        public List<SyncStorageState> Saved { get; } = [];

        public void Set(SyncStorageState state) => current = state;

        public Task<SyncStorageState?> GetAsync(string profile, string owner) => Task.FromResult(current);

        public Task SaveAsync(SyncStorageState state)
        {
            current = state;
            Saved.Add(state);
            return Task.CompletedTask;
        }
    }

    private sealed class Reporter(Action<SyncStorageMigrationProgress> report) : IProgress<SyncStorageMigrationProgress>
    {
        public void Report(SyncStorageMigrationProgress value) => report(value);
    }

    private sealed class CorruptingStore(ISyncFileStore inner) : ISyncFileStore
    {
        public bool FailDeletes { get; set; }

        public Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);

        public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
        {
            var file = await inner.ReadAsync(key, ct);
            return file == null ? null : new SyncFile { Key = key, Content = [.. file.Content, 0], ModifiedAt = file.ModifiedAt };
        }

        public Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default) => inner.WriteAsync(key, content, modifiedAt, ct);

        public Task CreateFolderAsync(string key, CancellationToken ct = default) => inner.CreateFolderAsync(key, ct);

        public Task DeleteAsync(string key, CancellationToken ct = default) =>
            FailDeletes ? throw new InvalidOperationException("delete failed") : inner.DeleteAsync(key, ct);
    }
}
