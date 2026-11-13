using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class SyncSchedulerTests
{
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<IBrowserNetworkStatus> mockBrowser = new();
    private readonly Mock<ISyncLockService> mockLocks = new();
    private readonly SyncOptions options = new();
    private readonly FakeSyncProfileRegistry registry;
    private readonly FakeTimeProvider time = new();
    private readonly List<string> calls = [];
    private ConnectivityStatus connectivity = new() { IsOnline = true };

    public SyncSchedulerTests()
    {
        registry = FakeSyncProfileRegistry.Of(options);
        mockSync.SetupGet(x => x.Profiles).Returns(["notes", "photos"]);
        mockSync.Setup(x => x.SyncAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, bool, IProgress<SyncPassProgress>?, CancellationToken>((p, v, _, _) => calls.Add($"sync {p} verify={v}"))
            .ReturnsAsync(new SyncResult());
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => connectivity);
        mockLocks.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add($"lock {name}"))
            .ReturnsAsync(() => new Releaser(calls));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(null!, registry, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, null!, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, registry, null!, mockBrowser.Object, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("browserNetworkStatus", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, registry, mockConnectivity.Object, null!, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, registry, mockConnectivity.Object, mockBrowser.Object, null!, options, time)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, registry, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, registry, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignIn_WhenStartAsync_ThenEveryProfileVerifiedUnderItsLock()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.True(sut.IsStarted);
        Assert.Equal(
            ["lock sync-notes", "sync notes verify=True", "release", "lock sync-photos", "sync photos verify=True", "release"],
            calls);
        mockConnectivity.Verify(x => x.InitializeAsync(), Times.Once);
        mockBrowser.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenStartAsync_ThenConnectivityCheckedWithoutSyncing()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.Empty(calls);
        mockConnectivity.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockSync.Verify(x => x.SyncAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenAppRegainsFocus_ThenConnectivityCheckedAgain()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        using var sut = CreateSut();
        await sut.StartAsync();

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);
        await WaitForAsync(() => mockConnectivity.Invocations.Count(i => i.Method.Name == nameof(IConnectivityService.CheckAsync)) >= 2);

        // Assert
        mockConnectivity.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenNoProfiles_AndConnectivityCheckThrows_WhenStartAsync_ThenStillStarted()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("probe failed"));
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.True(sut.IsStarted);
    }

    [Fact]
    public async Task GivenStarted_WhenStartAsyncAgain_ThenNothingHappens()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenStarted_WhenChangesRequested_ThenDebouncedIntoOnePassWithoutVerify()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        sut.RequestSync("notes");
        time.Advance(TimeSpan.FromSeconds(1));
        sut.RequestSync("notes");
        time.Advance(TimeSpan.FromSeconds(1.9));
        var beforeDebounce = calls.Count;
        time.Advance(TimeSpan.FromSeconds(0.1));
        await WaitForAsync(() => calls.Count == 3);

        // Assert
        Assert.Equal(0, beforeDebounce);
        Assert.Equal(["lock sync-notes", "sync notes verify=False", "release"], calls);
    }

    [Fact]
    public async Task GivenCustomDebounce_WhenChangeRequested_ThenItIsUsed()
    {
        // Arrange
        options.ChangeDebounce = TimeSpan.FromSeconds(10);
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        sut.RequestSync("notes");
        time.Advance(TimeSpan.FromSeconds(9));
        var early = calls.Count;
        time.Advance(TimeSpan.FromSeconds(1));
        await WaitForAsync(() => calls.Contains("sync notes verify=False"));

        // Assert
        Assert.Equal(0, early);
        Assert.Contains("sync notes verify=False", calls);
    }

    [Fact]
    public async Task GivenPassRunning_WhenMoreChangesRequested_ThenOneMorePassRunsAfterwards()
    {
        // Arrange
        var gate = new TaskCompletionSource<SyncResult>();
        var passes = 0;
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++passes == 2 ? gate.Task : Task.FromResult(new SyncResult()));
        using var sut = CreateSut();
        await sut.StartAsync();

        // Act
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        await Task.Yield();
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        var whileRunning = passes;
        gate.SetResult(new SyncResult());
        await WaitForAsync(() => passes == 3);

        // Assert
        Assert.Equal(2, whileRunning);
        Assert.Equal(3, passes);
    }

    [Fact]
    public async Task GivenOffline_WhenPassDue_ThenNoPassRunsAndPendingCountRefreshed()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.NoCredentials };
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        await Task.Yield();

        // Assert
        mockSync.Verify(x => x.SyncAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockLocks.VerifyNoOtherCalls();
        mockSync.Verify(x => x.RefreshPendingCountAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task GivenOfflineAtStart_WhenBackOnline_ThenEveryProfileSyncedWithVerifyStillPending()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.Unreachable };
        using var sut = CreateSut();
        await sut.StartAsync();
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);

        // Act
        connectivity = new ConnectivityStatus { IsOnline = true };
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
        await WaitForAsync(() => calls.Contains("sync photos verify=True"));

        // Assert
        Assert.Contains("sync notes verify=True", calls);
        Assert.Contains("sync photos verify=True", calls);
    }

    [Fact]
    public async Task GivenAlreadyOnline_WhenStatusChangedRaisedOnline_ThenNoExtraPass()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
        await Task.Yield();

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenStarted_WhenAppRegainsFocus_ThenEveryProfileSynced()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, mockBrowser.Object, EventArgs.Empty);
        await WaitForAsync(() => calls.Contains("sync photos verify=False"));

        // Assert
        Assert.Contains("sync notes verify=False", calls);
        Assert.Contains("sync photos verify=False", calls);
    }

    [Fact]
    public async Task GivenStarted_WhenSyncNowAsync_ThenEveryProfileSynced()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        await sut.SyncNowAsync();

        // Assert
        Assert.Equal(6, calls.Count);
    }

    [Fact]
    public async Task GivenStarted_WhenSyncProfileAsync_ThenOnlyThatProfileIsSyncedUnderItsLock()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        await sut.SyncProfileAsync("photos");

        // Assert
        Assert.Equal(["lock sync-photos", "sync photos verify=False", "release"], calls);
    }

    [Fact]
    public async Task GivenAPassOfTheProfileRunning_WhenSyncProfileAsync_ThenItWaitsForThatPassAndOneMore()
    {
        // Arrange
        var gate = new TaskCompletionSource<SyncResult>();
        var passes = 0;
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++passes == 2 ? gate.Task : Task.FromResult(new SyncResult()));
        using var sut = CreateSut();
        await sut.StartAsync();
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        await WaitForAsync(() => passes == 2);

        // Act
        var waiting = sut.SyncProfileAsync("notes");
        var finishedEarly = waiting.IsCompleted;
        gate.SetResult(new SyncResult());
        await waiting;

        // Assert
        Assert.False(finishedEarly);
        Assert.Equal(3, passes);
    }

    [Fact]
    public async Task GivenOffline_WhenSyncProfileAsync_ThenNothingSyncs()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        connectivity = new ConnectivityStatus { IsOnline = false };
        calls.Clear();

        // Act
        await sut.SyncProfileAsync("notes");

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenNotStartedOrAnUnknownProfile_WhenSyncProfileAsync_ThenNothingHappens()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.SyncProfileAsync("notes");
        await sut.StartAsync();
        calls.Clear();
        await sut.SyncProfileAsync("unknown");

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenNotStarted_WhenSyncRequested_ThenNothingHappens()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        sut.RequestSync("notes");
        time.Advance(TimeSpan.FromMinutes(1));
        await sut.SyncNowAsync();

        // Assert
        Assert.Empty(calls);
        Assert.False(sut.IsStarted);
    }

    [Fact]
    public async Task GivenPendingChange_WhenStopAsync_ThenDebounceCancelledAndEverythingReset()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();
        sut.RequestSync("notes");

        // Act
        await sut.StopAsync();
        time.Advance(TimeSpan.FromMinutes(1));
        mockBrowser.Raise(x => x.BecameVisible += null, mockBrowser.Object, EventArgs.Empty);
        await Task.Yield();

        // Assert
        Assert.Empty(calls);
        Assert.False(sut.IsStarted);
        mockConnectivity.Verify(x => x.Reset(), Times.Once);
        mockSync.Verify(x => x.Reset(), Times.Once);
        mockBrowser.Verify(x => x.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenNotStarted_WhenStopAsync_ThenNothingHappens()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.StopAsync();

        // Assert
        mockSync.Verify(x => x.Reset(), Times.Never);
    }

    [Fact]
    public async Task GivenPassThrows_WhenRunning_ThenLockReleasedAndSchedulerCarriesOn()
    {
        // Arrange
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.Equal(["lock sync-notes", "release", "lock sync-photos", "sync photos verify=True", "release"], calls);
    }

    [Fact]
    public async Task GivenStarted_WhenAProfileIsAdded_ThenItIsVerifiedAndThenSyncsOnRequest()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        registry.Add(new SyncProfileOptions { Name = "files", RemotePattern = "files/**/*" });
        await WaitForAsync(() => calls.Contains("release"));

        // Assert
        Assert.Equal(["lock sync-files", "sync files verify=True", "release"], calls);
    }

    [Fact]
    public async Task GivenStarted_WhenAnAddedProfileIsRemoved_ThenRequestsForItAreIgnored()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        registry.Add(new SyncProfileOptions { Name = "files", RemotePattern = "files/**/*" });
        await WaitForAsync(() => calls.Contains("sync files verify=True"));
        calls.Clear();

        // Act
        registry.Remove("files");
        sut.RequestSync("files");
        await sut.SyncNowAsync();

        // Assert
        Assert.DoesNotContain(calls, c => c.Contains("files"));
    }

    [Fact]
    public async Task GivenStopped_WhenAProfileIsAdded_ThenNothingRuns()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        await sut.StopAsync();
        calls.Clear();

        // Act
        registry.Add(new SyncProfileOptions { Name = "files", RemotePattern = "files/**/*" });
        await Task.Delay(20);

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public void GivenUnknownProfile_WhenRequestSync_ThenIgnored()
    {
        // Arrange
        using var sut = CreateSut();
        sut.RequestSync("unknown");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.RequestSync(" "));
    }

    [Fact]
    public async Task GivenNoProfiles_AndConnectivityCheckCancelled_WhenStartAsync_ThenStillStarted()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.True(sut.IsStarted);
        mockConnectivity.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoProfiles_AndStoppedWhileStarting_WhenStartFinishes_ThenConnectivityIsNotChecked()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns([]);
        var browserStarted = new TaskCompletionSource();
        mockBrowser.Setup(x => x.StartAsync()).Returns(browserStarted.Task);
        using var sut = CreateSut();
        var starting = sut.StartAsync();

        // Act
        await sut.StopAsync();
        browserStarted.SetResult();
        await starting;

        // Assert
        Assert.False(sut.IsStarted);
        mockConnectivity.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenProfiles_AndStoppedWhileStarting_WhenStartFinishes_ThenNothingSyncs()
    {
        // Arrange
        var browserStarted = new TaskCompletionSource();
        mockBrowser.Setup(x => x.StartAsync()).Returns(browserStarted.Task);
        using var sut = CreateSut();
        var starting = sut.StartAsync();

        // Act
        await sut.StopAsync();
        browserStarted.SetResult();
        await starting;

        // Assert
        Assert.Empty(calls);
        mockConnectivity.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockSync.Verify(x => x.RefreshPendingCountAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenPassCancelled_WhenRunning_ThenLockReleasedAndOtherProfilesStillSync()
    {
        // Arrange
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<IProgress<SyncPassProgress>?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();
        calls.Clear();
        await sut.SyncProfileAsync("notes");

        // Assert
        Assert.Equal(["lock sync-notes", "release"], calls);
        Assert.True(sut.IsStarted);
    }

    [Fact]
    public async Task GivenConnectivityCheckFails_WhenPassDue_ThenNothingSyncsAndTheNextTriggerTriesAgain()
    {
        // Arrange
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("probe failed"));
        using var sut = CreateSut();
        await sut.StartAsync();

        // Act
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => connectivity);
        await sut.SyncProfileAsync("notes");

        // Assert
        Assert.Equal(["lock sync-notes", "sync notes verify=True", "release"], calls);
    }

    [Fact]
    public async Task GivenStarted_WhenAnExistingProfileIsAddedAgain_ThenNoExtraPassRuns()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        calls.Clear();

        // Act
        registry.Add(new SyncProfileOptions { Name = "notes", RemotePattern = "notes/**/*" });
        await Task.Delay(20);

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenAPendingChange_WhenItsProfileIsRemoved_ThenTheDebouncedPassNeverRuns()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        registry.Add(new SyncProfileOptions { Name = "files", RemotePattern = "files/**/*" });
        await WaitForAsync(() => calls.Contains("sync files verify=True"));
        await WaitForAsync(() => calls.Count(c => c == "release") == 3);
        calls.Clear();
        sut.RequestSync("files");

        // Act
        registry.Remove("files");
        time.Advance(options.ChangeDebounce);
        await Task.Delay(20);

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenWentOfflineAndBackOnlineWhileOfflinePassesAreSkipped_WhenBackOnline_ThenPendingChangesSyncOnce()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.StartAsync();
        connectivity = new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.NoNetwork };
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
        sut.RequestSync("notes");
        time.Advance(options.ChangeDebounce);
        await WaitForAsync(() => mockSync.Invocations.Any(i => i.Method.Name == nameof(ISyncService.RefreshPendingCountAsync)));
        calls.Clear();

        // Act
        connectivity = new ConnectivityStatus { IsOnline = true };
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
        await WaitForAsync(() => calls.Contains("sync photos verify=False"));

        // Assert
        Assert.Equal(
            ["lock sync-notes", "sync notes verify=False", "release", "lock sync-photos", "sync photos verify=False", "release"],
            calls);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    private SyncScheduler CreateSut() =>
        new(mockSync.Object, registry, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, time);

    private sealed class Releaser(List<string> calls) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            calls.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}
