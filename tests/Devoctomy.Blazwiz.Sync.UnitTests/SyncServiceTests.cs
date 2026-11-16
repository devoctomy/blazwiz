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
        mockEngine.Setup(x => x.SyncAsync(It.IsAny<SyncProfile>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncProfile p, bool _, IProgress<SyncPassProgress>? _, CancellationToken _) => new SyncResult { Profile = p.Name });
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
        mockEngine.Verify(x => x.SyncAsync(notes, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockEngine.Verify(x => x.SyncAsync(photos, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([SyncState.Syncing, SyncState.Idle], states.Distinct());
        Assert.Equal(SyncState.Idle, states[^1]);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
        Assert.Null(sut.Status.LastErrorCode);
        mockLog.Verify(x => x.PruneAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenAPassWithChanges_WhenSyncAsync_ThenTheStatusShowsHowFarItHasGot_AndTheCallerGetsEveryReport()
    {
        // Arrange
        var times = new Queue<DateTimeOffset>([Now, Now.AddMilliseconds(100), Now.AddMilliseconds(300), Now.AddMilliseconds(400), Now.AddMilliseconds(500)]);
        mockClock.Setup(x => x.GetUtcNow()).Returns(() => times.Count > 0 ? times.Dequeue() : Now);
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<SyncProfile, bool, IProgress<SyncPassProgress>?, CancellationToken>((_, _, progress, _) =>
            {
                for (var done = 0; done <= 4; done++)
                {
                    progress!.Report(new SyncPassProgress(done, 4));
                }
            })
            .ReturnsAsync(new SyncResult { Profile = "notes" });
        var sut = CreateSut();
        var shown = new List<string>();
        sut.StatusChanged += (_, s) => shown.Add(Describe(s));
        var reported = new List<int>();

        // Act
        await sut.SyncAsync("notes", progress: new SyncProgressRecorder(reported));

        // Assert
        // 1 is within 250ms of 0 and skipped; 2 is 300ms after 0; 3 is within 250ms of 2; 4 ends the pass.
        Assert.Equal(["Syncing", "Syncing notes 0/0", "Syncing notes 0/4", "Syncing notes 2/4", "Syncing notes 4/4", "Syncing", "Idle"], shown);
        Assert.Equal([0, 1, 2, 3, 4], reported);
        Assert.Empty(sut.Status.Passes);
    }

    [Fact]
    public async Task GivenTwoProfilesSyncingAtOnce_WhenBothReportProgress_ThenTheStatusShowsEachOnesOwnProgress()
    {
        // Arrange
        SyncService? sut = null;
        IReadOnlyList<SyncPassStatus>? during = null;
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<SyncProfile, bool, IProgress<SyncPassProgress>?, CancellationToken>((_, _, progress, _) =>
            {
                progress!.Report(new SyncPassProgress(3, 4));
                during = sut!.Status.Passes;
            })
            .ReturnsAsync(new SyncResult { Profile = "notes" });
        sut = CreateSut();
        var release = new TaskCompletionSource();
        var photosDeleted = sut.TrackAsync("photos", async progress =>
        {
            progress.Report(new SyncPassProgress(1, 5));
            await release.Task;
        });

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.NotNull(during);
        Assert.Equal(["photos 1/5", "notes 3/4"], during.Select(p => $"{p.Profile} {p.Done}/{p.Total}"));
        Assert.Equal(["photos 1/5"], sut.Status.Passes.Select(p => $"{p.Profile} {p.Done}/{p.Total}"));
        release.SetResult();
        await photosDeleted;
        Assert.Empty(sut.Status.Passes);
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    [Fact]
    public async Task GivenAPassWithNothingToChange_WhenSyncAsync_ThenTheStatusShowsNoCount()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<SyncProfile, bool, IProgress<SyncPassProgress>?, CancellationToken>((_, _, progress, _) => progress!.Report(new SyncPassProgress(0, 0)))
            .ReturnsAsync(new SyncResult { Profile = "notes" });
        var sut = CreateSut();
        var shown = new List<string>();
        sut.StatusChanged += (_, s) => shown.Add(Describe(s));

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(["Syncing", "Syncing notes 0/0", "Syncing", "Idle"], shown);
    }

    [Fact]
    public async Task GivenTrackedWork_WhenItRuns_ThenTheStatusSaysSyncingWithItsCount_ThenGoesBackToIdle()
    {
        // Arrange
        var sut = CreateSut();
        var shown = new List<string>();
        sut.StatusChanged += (_, s) => shown.Add(Describe(s));

        // Act
        await sut.TrackAsync("notes", progress =>
        {
            progress.Report(new SyncPassProgress(0, 2));
            progress.Report(new SyncPassProgress(2, 2));
            return Task.CompletedTask;
        });

        // Assert
        Assert.Equal(["Syncing", "Syncing notes 0/0", "Syncing notes 0/2", "Syncing notes 2/2", "Syncing", "Idle"], shown);
    }

    [Fact]
    public async Task GivenTrackedWorkThatFails_WhenItRuns_ThenItThrows_AndTheStatusGoesBackToIdle()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.TrackAsync("notes", _ => throw new InvalidOperationException("failed")));
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Empty(sut.Status.Passes);
    }

    [Fact]
    public async Task GivenTrackedWorkStillRunning_WhenAPassEnds_ThenTheStatusStillSaysSyncing()
    {
        // Arrange
        var sut = CreateSut();
        var release = new TaskCompletionSource();
        var tracked = sut.TrackAsync("photos", _ => release.Task);

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncState.Syncing, sut.Status.State);
        release.SetResult();
        await tracked;
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    [Fact]
    public async Task GivenNoWork_WhenTracked_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().TrackAsync("notes", null!));
    }

    [Fact]
    public async Task GivenNoProfile_WhenTracked_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().TrackAsync(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task GivenOneProfile_WhenSyncAsync_ThenOnlyThatProfileRunsWithVerify()
    {
        // Act
        await CreateSut().SyncAsync("photos", true);

        // Assert
        mockEngine.Verify(x => x.SyncAsync(photos, true, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
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
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 2 });
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
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
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
        mockEngine.SetupSequence(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
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
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new SyncAccessDeniedException("403"));
        var sut = CreateSut();

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncErrorCodes.RemoteAccessDenied, sut.Status.LastErrorCode);
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenCancelled_WhenSyncAsync_ThenIdleWithoutError()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
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
        mockEngine.Verify(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockEngine.Verify(x => x.SyncAsync(photos, false, It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
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
        mockEngine.Verify(x => x.SyncAsync(It.IsAny<SyncProfile>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(sut.Status.LastSyncedAt);
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    [Fact]
    public async Task GivenOneProfileFailed_WhenAnotherSyncsAfterIt_ThenStatusIsStillTheError()
    {
        // Arrange: one profile at a time, as the scheduler syncs them.
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 3 });
        var sut = CreateSut();
        await sut.SyncAsync("notes");

        // Act
        await sut.SyncAsync("photos");

        // Assert
        Assert.Equal(SyncState.Error, sut.Status.State);
        Assert.Equal(SyncErrorCodes.FilesFailed, sut.Status.LastErrorCode);
        Assert.Equal([3], sut.Status.LastErrorArguments);
        Assert.Null(sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenAProfileFailed_WhenItSyncsAgainSuccessfully_ThenIdle()
    {
        // Arrange
        mockEngine.SetupSequence(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncNetworkException("down"))
            .ReturnsAsync(new SyncResult());
        var sut = CreateSut();
        await sut.SyncAsync("notes");
        await sut.SyncAsync("photos");

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Null(sut.Status.LastErrorCode);
        Assert.Empty(sut.Status.LastErrorArguments);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenProfilesSyncedAtDifferentTimes_WhenStatusRead_ThenLastSyncedIsTheLeastRecent_AndConflictsAddUp()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { ConflictsResolved = 1 });
        mockEngine.Setup(x => x.SyncAsync(photos, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { ConflictsResolved = 2 });
        var sut = CreateSut();
        await sut.SyncAsync("notes");
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now.AddMinutes(5));

        // Act
        await sut.SyncAsync("photos");

        // Assert
        Assert.Equal(Now, sut.Status.LastSyncedAt);
        Assert.Equal(3, sut.Status.ConflictsResolved);
    }

    [Fact]
    public async Task GivenAFailedProfile_WhenAGateHoldsItBack_ThenItNoLongerCounts()
    {
        // Arrange
        var held = false;
        var gate = new Mock<ISyncGate>();
        gate.Setup(x => x.CanSyncAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string p, CancellationToken _) => p != "notes" || !held);
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 1 });
        var sut = new SyncService(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [gate.Object]);
        await sut.SyncAsync();
        held = true;

        // Act
        await sut.SyncAsync();

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenAFailedProfile_WhenItIsRemoved_ThenItNoLongerCounts()
    {
        // Arrange
        var registry = FakeSyncProfileRegistry.Of(notes);
        registry.Add(photos);
        mockEngine.Setup(x => x.SyncAsync(photos, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 1 });
        var sut = new SyncService(registry, mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, []);
        await sut.SyncAsync();
        registry.Remove("photos");

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenAProfileFailed_WhenReset_ThenTheFailureIsForgotten()
    {
        // Arrange
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult { Failed = 1 });
        var sut = CreateSut();
        await sut.SyncAsync("notes");
        sut.Reset();

        // Act
        await sut.SyncAsync("photos");

        // Assert
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public void GivenNullGates_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("gates", Assert.Throws<ArgumentNullException>(() => new SyncService(FakeSyncProfileRegistry.Of(), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenCountingPendingChangesFails_WhenRefreshPendingCountAsync_ThenTheLastCountIsKept()
    {
        // Arrange
        await notesIndex.SaveAsync(new SyncIndexRecord { Profile = "notes", Owner = Owner, Key = "notes/a.md", Dirty = true });
        var sut = CreateSut();
        await sut.RefreshPendingCountAsync();
        mockUser.Setup(x => x.GetUserIdAsync()).ThrowsAsync(new InvalidOperationException("storage gone"));
        var raised = 0;
        sut.StatusChanged += (_, _) => raised++;

        // Act
        await sut.RefreshPendingCountAsync();

        // Assert
        Assert.Equal(1, sut.Status.PendingCount);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task GivenCountingPendingChangesFails_WhenSyncAsync_ThenThePassStillCompletes()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ThrowsAsync(new InvalidOperationException("storage gone"));
        var sut = CreateSut();

        // Act
        var result = await sut.SyncAsync();

        // Assert
        Assert.Empty(result.Errors);
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Equal(0, sut.Status.PendingCount);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenAGateCannotReachTheNetwork_WhenSyncAsync_ThenConnectivityIsToldAndNoProfileIsMarkedFailed()
    {
        // Arrange
        var failure = new SyncNetworkException(SyncErrorCodes.RemoteUnreachable);
        var gate = new Mock<ISyncGate>();
        gate.Setup(x => x.CanSyncAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sut = new SyncService(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, [gate.Object]);

        // Act
        var result = await sut.SyncAsync();

        // Assert
        mockConnectivity.Verify(x => x.ReportNetworkFailure(failure), Times.Once);
        Assert.Contains(failure.Message, result.Errors);
        Assert.Equal(SyncState.Idle, sut.Status.State);
        Assert.Null(sut.Status.LastErrorCode);
        mockEngine.Verify(x => x.SyncAsync(It.IsAny<SyncProfile>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAProfileSyncedThenWentOffline_WhenItFails_ThenItsLastSyncTimeIsKept()
    {
        // Arrange
        mockEngine.SetupSequence(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult())
            .ThrowsAsync(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable));
        var sut = new SyncService(FakeSyncProfileRegistry.Of(notes), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, []);
        await sut.SyncAsync("notes");
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now.AddMinutes(5));

        // Act
        await sut.SyncAsync("notes");

        // Assert
        Assert.Equal(SyncState.Error, sut.Status.State);
        Assert.Equal(SyncErrorCodes.RemoteUnreachable, sut.Status.LastErrorCode);
        Assert.Equal(Now, sut.Status.LastSyncedAt);
    }

    [Fact]
    public async Task GivenDownloadsButNoListener_WhenSyncAsync_ThenThePassCompletes()
    {
        // Arrange
        var changed = new SyncResult();
        changed.AddChangedLocalKey("notes", "notes/a.md");
        mockEngine.Setup(x => x.SyncAsync(notes, It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(changed);
        var sut = CreateSut();

        // Act
        var result = await sut.SyncAsync();

        // Assert
        Assert.Equal(["notes/a.md"], result.ChangedLocalKeys["notes"]);
        Assert.Equal(SyncState.Idle, sut.Status.State);
    }

    private SyncService CreateSut() =>
        new(FakeSyncProfileRegistry.Of(notes, photos), mockEngine.Object, mockTargets.Object, mockConnectivity.Object, mockUser.Object, mockLog.Object, mockClock.Object, []);

    /// <summary>
    /// The state and each running pass, e.g. "Syncing notes 2/4".
    /// </summary>
    private static string Describe(SyncStatus status) =>
        string.Join(" ", new[] { status.State.ToString() }.Concat(status.Passes.Select(p => $"{p.Profile} {p.Done}/{p.Total}")));

    /// <summary>
    /// Records each report as it is made (unlike <see cref="Progress{T}"/>, which posts them).
    /// </summary>
    private sealed class SyncProgressRecorder(List<int> done)
        : IProgress<SyncPassProgress>
    {
        public void Report(SyncPassProgress value) => done.Add(value.Done);
    }
}
