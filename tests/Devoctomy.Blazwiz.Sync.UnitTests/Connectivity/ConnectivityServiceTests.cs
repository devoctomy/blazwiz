using Amazon.S3;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Connectivity;

public class ConnectivityServiceTests
{
    private readonly OfflineModeService offlineMode = new();
    private readonly Mock<ISyncEnabledStore> mockSyncStore = new();
    private readonly Mock<IBrowserNetworkStatus> mockBrowser = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly Mock<IS3ReachabilityProbe> mockProbe = new();
    private readonly Mock<ISyncEventLog> mockLog = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly SyncOptions options = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly S3SyncTarget target = new(Mock.Of<IAmazonS3>(), "bucket", "id/");
    private readonly List<SyncEvent> logged = [];

    public ConnectivityServiceTests()
    {
        mockSyncStore.Setup(x => x.GetAsync()).ReturnsAsync(true);
        mockBrowser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(target);
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLog.Setup(x => x.WriteAsync(It.IsAny<IEnumerable<SyncEvent>>())).Callback<IEnumerable<SyncEvent>>(e => logged.AddRange(e)).Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(null!, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, options, time)).ParamName);
        Assert.Equal("syncEnabledStore", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, null!, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, options, time)).ParamName);
        Assert.Equal("browserNetworkStatus", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, null!, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, options, time)).ParamName);
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, null!, mockProbe.Object, mockLog.Object, mockUser.Object, options, time)).ParamName);
        Assert.Equal("reachabilityProbe", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, null!, mockLog.Object, mockUser.Object, options, time)).ParamName);
        Assert.Equal("eventLog", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, null!, mockUser.Object, options, time)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, null!, options, time)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new ConnectivityService(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, options, null!)).ParamName);
    }

    [Fact]
    public async Task GivenEverythingWorks_WhenCheckAsync_ThenOnline()
    {
        // Arrange
        using var sut = CreateSut();
        ConnectivityStatus? raised = null;
        sut.StatusChanged += (_, s) => raised = s;

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.True(result.IsOnline);
        Assert.Null(result.Reason);
        Assert.Equal(time.GetUtcNow(), result.LastCheckedAt);
        Assert.True(raised!.IsOnline);
        Assert.Empty(logged);
        mockProbe.Verify(x => x.IsReachableAsync(target, options.ConnectivityTimings.ProbeTimeout, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenSyncTurnedOff_WhenCheckAsync_ThenSyncDisabledWithNoNetworkCalls()
    {
        // Arrange
        mockSyncStore.Setup(x => x.GetAsync()).ReturnsAsync(false);
        using var sut = CreateSut();

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.SyncDisabled, result.Reason);
        Assert.False(sut.IsSyncEnabled);
        mockBrowser.Verify(x => x.IsOnlineAsync(), Times.Never);
        mockTargets.Verify(x => x.GetTargetAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockProbe.VerifyNoOtherCalls();
        Assert.False(sut.IsRecheckScheduled);
    }

    [Fact]
    public async Task GivenBrowserOffline_WhenCheckAsync_ThenNoNetworkWithoutAskingForCredentials()
    {
        // Arrange
        mockBrowser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
        using var sut = CreateSut();

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.NoNetwork, result.Reason);
        mockTargets.Verify(x => x.GetTargetAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(SyncEventCodes.WentOffline, Assert.Single(logged).Code);
        Assert.Equal([nameof(OfflineReason.NoNetwork)], logged[0].Arguments);
    }

    [Fact]
    public async Task GivenNoCredentials_WhenCheckAsync_ThenNoCredentialsWithoutProbing()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((S3SyncTarget?)null);
        using var sut = CreateSut();

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.NoCredentials, result.Reason);
        mockProbe.VerifyNoOtherCalls();
        var entry = Assert.Single(logged);
        Assert.Equal(SyncEventKind.WentOffline, entry.Kind);
        Assert.Equal(SyncEventCodes.WentOffline, entry.Code);
        Assert.Equal([nameof(OfflineReason.NoCredentials)], entry.Arguments);
    }

    [Fact]
    public async Task GivenCredentialServiceUnreachable_WhenCheckAsync_ThenUnreachable()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        using var sut = CreateSut();

        // Act & Assert
        Assert.Equal(OfflineReason.Unreachable, (await sut.CheckAsync()).Reason);
    }

    [Fact]
    public async Task GivenS3Unreachable_WhenCheckAsync_ThenUnreachableAndRecheckScheduled()
    {
        // Arrange
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        using var sut = CreateSut();

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.Unreachable, result.Reason);
        Assert.True(sut.IsRecheckScheduled);
    }

    [Fact]
    public async Task GivenRecentCheck_WhenCheckAsync_ThenCachedResultReusedUntilCacheExpires()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        time.Advance(TimeSpan.FromSeconds(29));
        await sut.CheckAsync();
        time.Advance(TimeSpan.FromSeconds(2));
        await sut.CheckAsync();

        // Assert
        mockProbe.Verify(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenRecentCheck_WhenRecheckAsync_ThenChecksAgain()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.RecheckAsync();

        // Assert
        mockProbe.Verify(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenUnreachable_WhenTimePasses_ThenRechecksWithBackoffAndLastDelayRepeats()
    {
        // Arrange
        var probes = 0;
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => probes++)
            .ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act & Assert (30s, 1m, 2m, 5m, 5m)
        time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(1, probes);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, probes);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(3, probes);
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(4, probes);
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(5, probes);
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(6, probes);
    }

    [Fact]
    public async Task GivenOffline_WhenRecheckSucceeds_ThenBackOnlineLoggedAndRaised()
    {
        // Arrange
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.CheckAsync();
        var raised = new List<ConnectivityStatus>();
        sut.StatusChanged += (_, s) => raised.Add(s);
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        time.Advance(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(Assert.Single(raised).IsOnline);
        Assert.Equal(SyncEventKind.WentOnline, logged[^1].Kind);
        Assert.Equal(SyncEventCodes.BackOnline, logged[^1].Code);
        Assert.False(sut.IsRecheckScheduled);
    }

    [Fact]
    public async Task GivenOnline_WhenReportNetworkFailure_ThenUnreachableAndRecheckScheduled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        sut.ReportNetworkFailure(new HttpRequestException());

        // Assert
        Assert.Equal(OfflineReason.Unreachable, sut.Status.Reason);
        Assert.True(sut.IsRecheckScheduled);
        await Task.Yield();
        Assert.Contains(logged, e => e.Kind == SyncEventKind.WentOffline);
    }

    [Fact]
    public async Task GivenOnline_WhenSyncTurnedOff_ThenSavedLoggedAndSyncDisabled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.SetSyncEnabledAsync(false);

        // Assert
        mockSyncStore.Verify(x => x.SetAsync(false), Times.Once);
        Assert.Equal(OfflineReason.SyncDisabled, sut.Status.Reason);
        var entry = Assert.Single(logged);
        Assert.Equal(SyncEventKind.OfflineModeChanged, entry.Kind);
        Assert.Equal(SyncEventCodes.SyncTurnedOff, entry.Code);
    }

    [Fact]
    public async Task GivenSyncTurnedOff_WhenTurnedOn_ThenCheckedAgainImmediately()
    {
        // Arrange
        mockSyncStore.Setup(x => x.GetAsync()).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.SetSyncEnabledAsync(true);

        // Assert
        Assert.True(sut.Status.IsOnline);
        Assert.Contains(logged, e => e.Code == SyncEventCodes.SyncTurnedOn);
        Assert.Contains(logged, e => e.Kind == SyncEventKind.WentOnline);
    }

    [Fact]
    public async Task GivenSyncUnchanged_WhenSetSyncEnabledAsync_ThenNotLoggedAgain()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.SetSyncEnabledAsync(true);

        // Assert
        Assert.DoesNotContain(logged, e => e.Kind == SyncEventKind.OfflineModeChanged);
    }

    [Fact]
    public async Task GivenCachedOnlineStatus_WhenSyncTurnedOff_ThenCacheIsNotUsed()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();
        await sut.SetSyncEnabledAsync(false);

        // Act
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.SyncDisabled, result.Reason);
    }

    [Fact]
    public async Task GivenOnline_WhenOfflineModeEntered_ThenOfflineModeWithNoNetworkCallsAndLogged()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();
        mockBrowser.Invocations.Clear();
        mockTargets.Invocations.Clear();

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var result = await sut.CheckAsync();

        // Assert
        Assert.Equal(OfflineReason.OfflineMode, result.Reason);
        Assert.Contains(logged, e => e.Code == SyncEventCodes.OfflineModeOn);
        mockBrowser.Verify(x => x.IsOnlineAsync(), Times.Never);
        mockTargets.Verify(x => x.GetTargetAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsRecheckScheduled);
    }

    [Fact]
    public async Task GivenOfflineModeAndSyncOff_WhenCheckAsync_ThenOfflineModeIsTheReason()
    {
        // Arrange
        mockSyncStore.Setup(x => x.GetAsync()).ReturnsAsync(false);
        offlineMode.Enter(OfflineEntryReason.SignInUnreachable);

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.Equal(OfflineReason.OfflineMode, (await sut.CheckAsync()).Reason);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenExited_ThenCheckedAgainAndOnline()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        offlineMode.Exit();

        // Assert
        Assert.True(sut.Status.IsOnline);
        Assert.Contains(logged, e => e.Code == SyncEventCodes.OfflineModeOff);
    }

    [Fact]
    public void GivenDisposed_WhenOfflineModeEntered_ThenNotChecked()
    {
        // Arrange
        var sut = CreateSut();
        sut.Dispose();

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Assert
        Assert.Empty(logged);
    }

    [Fact]
    public async Task GivenBrowserGoesOnline_WhenEventRaised_ThenChecksAgain()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        mockBrowser.Raise(x => x.OnlineChanged += null, mockBrowser.Object, true);
        await Task.Yield();

        // Assert
        mockProbe.Verify(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenSignedOut_WhenGoingOffline_ThenNothingLogged()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        mockBrowser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
        using var sut = CreateSut();

        // Act
        await sut.CheckAsync();

        // Assert
        Assert.Empty(logged);
    }

    [Fact]
    public async Task GivenOfflineWithRecheck_WhenReset_ThenRecheckStoppedAndStatusForgotten()
    {
        // Arrange
        mockProbe.Setup(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        sut.Reset();
        time.Advance(TimeSpan.FromMinutes(10));

        // Assert
        Assert.False(sut.IsRecheckScheduled);
        Assert.Null(sut.Status.LastCheckedAt);
        mockProbe.Verify(x => x.IsReachableAsync(target, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenLogFails_WhenGoingOffline_ThenStatusStillUpdated()
    {
        // Arrange
        mockLog.Setup(x => x.WriteAsync(It.IsAny<IEnumerable<SyncEvent>>())).ThrowsAsync(new InvalidOperationException());
        mockBrowser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
        using var sut = CreateSut();

        // Act & Assert
        Assert.Equal(OfflineReason.NoNetwork, (await sut.CheckAsync()).Reason);
    }

    private ConnectivityService CreateSut() =>
        new(offlineMode, mockSyncStore.Object, mockBrowser.Object, mockTargets.Object, mockProbe.Object, mockLog.Object, mockUser.Object, options, time);
}
