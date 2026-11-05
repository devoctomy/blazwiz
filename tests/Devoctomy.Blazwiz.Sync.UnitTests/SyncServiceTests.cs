using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class SyncServiceTests
{
    private const string Owner = "bob";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISyncEngine> mockEngine = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISyncEventLog> mockLog = new();
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly InMemorySyncIndexStore notesIndex = new();
    private readonly SyncProfile notes;
    private readonly SyncProfile photos;

    public SyncServiceTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
        mockEngine.Setup(x => x.SyncAsync(It.IsAny<SyncProfile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncProfile p, bool _, CancellationToken _) => new SyncResult { Profile = p.Name });
        notes = Profile("notes", notesIndex, "notes/**/*.md");
        photos = Profile("photos", new InMemorySyncIndexStore(), "photos/**/*.png");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncService(null!, mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [])).ParamName);
        Assert.Equal("engine", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), null!, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [])).ParamName);
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, null!, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [])).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, null!, mockUser.Object, mockLog.Object, mockClock.Object, [])).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, null!, mockLog.Object, mockClock.Object, [])).ParamName);
        Assert.Equal("eventLog", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, null!, mockClock.Object, [])).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, null!, [])).ParamName);
    }

    [Fact]
    public async Task GivenProfiles_WhenSyncAsyncForAll_ThenEachRunsAndStatusUpdatedAndLogPruned()
    {
        // Arrange
        var sut = CreateSut();
        var states = new List<SyncState>();
        sut.StatusChanged += (_, s) => states.Add(s.State);

        // Act
        await sut.SyncAsync();

        // Assert
        Assert.Equal(["notes", "photos"], sut.Profiles);
        mockEngine.Verify(x => x.SyncAsync(notes, false, It.IsAny<CancellationToken>()), Times.Once);
        mockEngine.Verify(x => x.SyncAsync(photos, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([SyncState.Syncing, SyncState.Idle], states);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
        Assert.Null(sut.Status.LastErrorCode);
        mockLog.Verify(x => x.PruneAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenOneProfile_WhenSyncAsync_ThenOnlyThatProfileRunsWithVerify()
    {
        // Act
        await CreateSut().SyncAsync("photos", true);

        // Assert
        mockEngine.Verify(x => x.SyncAsync(photos, true, It.IsAny<CancellationToken>()), Times.Once);
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenUnknownProfile_WhenSyncAsync_ThenThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().SyncAsync("unknown"));
    }

    [Fact]
    public async Task GivenDownloads_WhenSyncAsync_ThenFilesChangedRaisedPerProfile()
    {
        // Arrange
        var result = new SyncResult();
        result.AddChangedLocalKey("notes", "notes/a.md");
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        var sut = CreateSut();
        SyncFilesChangedEventArgs? raised = null;
        sut.FilesChanged += (_, e) => raised = e;

        // Act
        await sut.SyncAsync();

        // Assert
        Assert.Equal("notes", raised!.Profile);
        Assert.Equal(["notes/a.md"], raised.Keys);
    }

    [Fact]
    public async Task GivenFileFailures_WhenSyncAsync_ThenErrorStatus()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 2 });
        var sut = CreateSut();

        // Act
        await sut.SyncAsync();

        // Assert
        Assert.Equal(SyncState.Error, sut.Status.State);
        Assert.Equal(SyncErrorCodes.FilesFailed, sut.Status.LastErrorCode);
        Assert.Equal([2], sut.Status.LastErrorArguments);
        Assert.Null(sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenS3Unreachable_WhenSyncAsync_ThenConnectivityToldAndErrorStatus()
    {
        // Arrange
        var failure = new SyncNetworkException("down");
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sut = CreateSut();

        // Act
        var result = await sut.SyncAsync();

        // Assert
        mockConnectivity.Verify(x => x.ReportNetworkFailure(failure), Times.Once);
        Assert.Equal(SyncState.Error, sut.Status.State);
        Assert.Equal(SyncErrorCodes.RemoteUnreachable, sut.Status.LastErrorCode);
        Assert.Contains("down", result.Errors);
    }

    [Fact]
    public async Task GivenCredentialsRefusedOnce_WhenSyncAsync_ThenRefreshedAndRetried()
    {
        // Arrange
        mockEngine.SetupSequence(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncAccessDeniedException("403"))
            .ReturnsAsync(new SyncResult());
        var sut = CreateSut();

        // Act
        await sut.SyncAsync("notes");

        // Assert
        mockTargets.Verify(x => x.Invalidate(), Times.Once);
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    [Fact]
    public async Task GivenCredentialsRefusedTwice_WhenSyncAsync_ThenPassFails()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new SyncAccessDeniedException("403"));
        var sut = CreateSut();

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncErrorCodes.RemoteAccessDenied, sut.Status.LastErrorCode);
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenCancelled_WhenSyncAsync_ThenIdleWithoutError()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act
        await sut.SyncAsync("notes", ct: source.Token);

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Null(sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenPendingRecords_WhenRefreshPendingCountAsync_ThenCountsDirtyAndDeletedMatchingRecords()
    {
        // Arrange
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = Owner, Key = "notes/a.md", Dirty = true });
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = Owner, Key = "notes/b.md", Deleted = true });
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = Owner, Key = "notes/c.md" });
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = Owner, Key = "notes/d.txt", Dirty = true });
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = "alice", Key = "notes/e.md", Dirty = true });
        var sut = CreateSut();
        var raised = 0;
        sut.StatusChanged += (_, _) => raised++;

        // Act
        await sut.RefreshPendingCountAsync();
        await sut.RefreshPendingCountAsync();

        // Assert
        Assert.Equal(2, sut.Status.PendingCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenSignedOut_WhenRefreshPendingCountAsync_ThenZero()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        var sut = CreateSut();

        // Act
        await sut.RefreshPendingCountAsync();

        // Assert
        Assert.Equal(0, sut.Status.PendingCount);
    }

    [Fact]
    public async Task GivenStatus_WhenReset_ThenClearedAndRaised()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SyncAsync();
        SyncStatus? raised = null;
        sut.StatusChanged += (_, s) => raised = s;

        // Act
        sut.Reset();

        // Assert
        Assert.Null(sut.Status.LastSyncedAt);
        Assert.NotNull(raised);
    }

    [Fact]
    public async Task GivenPruneFails_WhenSyncAsync_ThenPassStillSucceeds()
    {
        // Arrange
        mockLog.Setup(x => x.PruneAsync()).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        // Act
        await sut.SyncAsync();

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    private static SyncProfile Profile(string name, ISyncIndexStore index, string pattern) =>
        new(name, Mock.Of<ISyncFileStore>(), index, Mock.Of<ISyncRemoteStore>(), new GlobSyncPathMatcher(pattern, true));

    [Fact]
    public async Task GivenGateHoldingProfile_WhenSyncAsync_ThenThatProfileIsSkipped()
    {
        // Arrange
        var gate = new Mock<ISyncGate>();
        gate.Setup(x => x.CanSyncAsync("notes", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        gate.Setup(x => x.CanSyncAsync("photos", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new SyncService(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [gate.Object]);

        // Act
        await sut.SyncAsync();

        // Assert
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        mockEngine.Verify(x => x.SyncAsync(photos, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenGateHoldingEveryProfile_WhenSyncAsync_ThenNothingRunsAndLastSyncedUnchanged()
    {
        // Arrange
        var gate = new Mock<ISyncGate>();
        var sut = new SyncService(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [gate.Object]);

        // Act
        await sut.SyncAsync();

        // Assert
        mockEngine.Verify(x => x.SyncAsync(It.IsAny<SyncProfile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(sut.Status.LastSyncedAt);
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    [Fact]
    public void GivenNullGates_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("gates", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, null!)).ParamName);
    }

    private SyncService CreateSut() =>
        new(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, []);
}
