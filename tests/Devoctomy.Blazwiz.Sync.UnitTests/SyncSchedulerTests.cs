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
    private readonly FakeTimeProvider time = new();
    private readonly List<string> calls = [];
    private ConnectivityStatus connectivity = new() { IsOnline = true };

    public SyncSchedulerTests()
    {
        mockSync.SetupGet(x => x.Profiles).Returns(["notes", "photos"]);
        mockSync.Setup(x => x.SyncAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string?, bool, CancellationToken>((p, v, _) => calls.Add($"sync {p} verify={v}"))
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
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(null!, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, null!, mockBrowser.Object, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("browserNetworkStatus", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, mockConnectivity.Object, null!, mockLocks.Object, options, time)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, mockConnectivity.Object, mockBrowser.Object, null!, options, time)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SyncScheduler(mockSync.Object, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, null!)).ParamName);
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
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
        mockSync.Verify(x => x.SyncAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
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
        mockSync.Setup(x => x.SyncAsync("notes", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        using var sut = CreateSut();

        // Act
        await sut.StartAsync();

        // Assert
        Assert.Equal(["lock sync-notes", "release", "lock sync-photos", "sync photos verify=True", "release"], calls);
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    private SyncScheduler CreateSut() =>
        new(mockSync.Object, mockConnectivity.Object, mockBrowser.Object, mockLocks.Object, options, time);

    private sealed class Releaser(List<string> calls) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            calls.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}
