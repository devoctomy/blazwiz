using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;
using Devoctomy.Blazwiz.Events.Sessions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Events.UnitTests.Sessions;

public class UserEventsSessionHandlerTests
{
    private readonly Mock<IUserEventChannel> mockChannel = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ISessionResumeMonitor> mockMonitor = new();
    private readonly Mock<IDeviceNameService> mockDeviceName = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly FakeTimeProvider time = new();
    private readonly UserSession session = new() { Username = "bob", ExpiresAt = DateTimeOffset.MaxValue };
    private UserEventConnectionState state = UserEventConnectionState.Disconnected;

    public UserEventsSessionHandlerTests()
    {
        mockChannel.Setup(x => x.State).Returns(() => state);
        mockChannel.Setup(x => x.ConnectAsync(It.IsAny<UserSession>())).Callback(() => state = UserEventConnectionState.Connected).Returns(Task.CompletedTask);
        mockChannel.Setup(x => x.DisconnectAsync()).Callback(() => state = UserEventConnectionState.Disconnected).Returns(Task.CompletedTask);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        mockDeviceName.Setup(x => x.Sanitize(It.IsAny<string?>())).Returns((string? name) => name?.Trim());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("channel", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(null!, mockAuth.Object, mockMonitor.Object, offlineMode, mockDeviceName.Object, time)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(mockChannel.Object, null!, mockMonitor.Object, offlineMode, mockDeviceName.Object, time)).ParamName);
        Assert.Equal("resumeMonitor", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(mockChannel.Object, mockAuth.Object, null!, offlineMode, mockDeviceName.Object, time)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(mockChannel.Object, mockAuth.Object, mockMonitor.Object, null!, mockDeviceName.Object, time)).ParamName);
        Assert.Equal("deviceNameService", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(mockChannel.Object, mockAuth.Object, mockMonitor.Object, offlineMode, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new UserEventsSessionHandler(mockChannel.Object, mockAuth.Object, mockMonitor.Object, offlineMode, mockDeviceName.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenOnlineSession_WhenSessionStarts_ThenTheChannelConnects()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OnSessionStartedAsync(session);

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(session), Times.Once);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenSessionStarts_ThenNothingConnects()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OnSessionStartedAsync(new UserSession { Username = "bob", IsOffline = true });

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Never);
    }

    [Theory]
    [InlineData(UserEventReasons.UserRequested, SessionEndedReason.SignedOutEverywhere)]
    [InlineData(UserEventReasons.KeysRotated, SessionEndedReason.KeysRotated)]
    [InlineData(null, SessionEndedReason.SignedOutEverywhere)]
    public async Task GivenConnected_WhenSignedOutEverywhereArrives_ThenTheSessionEndsWithTheReasonAndSanitisedDevice(string? reason, SessionEndedReason expected)
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        var ended = new TaskCompletionSource();
        mockAuth.Setup(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>())).Callback(() => ended.TrySetResult()).Returns(Task.CompletedTask);

        // Act
        mockChannel.Raise(x => x.Received += null, mockChannel.Object, new UserEvent { Type = UserEventTypes.SignedOutEverywhere, Reason = reason, Device = " Phone ", From = "other" });

        // Assert
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        mockAuth.Verify(x => x.EndSessionAsync(expected, "Phone"), Times.Once);
    }

    [Fact]
    public async Task GivenConnected_WhenAnUnknownEventArrives_ThenNothingHappens()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);

        // Act
        mockChannel.Raise(x => x.Received += null, mockChannel.Object, new UserEvent { Type = "something-else", From = "other" });

        // Assert
        mockAuth.Verify(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenConnected_WhenSigningOut_ThenDisconnects_AndEventsAreNoLongerHandled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);

        // Act
        await sut.OnSigningOutAsync();
        mockChannel.Raise(x => x.Received += null, mockChannel.Object, new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = "other" });

        // Assert
        mockChannel.Verify(x => x.DisconnectAsync(), Times.Once);
        mockAuth.Verify(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenConnectFails_WhenSessionStarts_ThenItRetriesWithBackoff()
    {
        // Arrange
        var attempts = 0;
        mockChannel.Setup(x => x.ConnectAsync(It.IsAny<UserSession>()))
            .Callback(() => attempts++)
            .Returns(() => attempts < 3 ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask);
        using var sut = CreateSut();

        // Act
        await sut.OnSessionStartedAsync(session);
        time.Advance(UserEventsSessionHandler.FirstReconnectDelay);
        await WaitForAsync(() => attempts == 2);
        time.Advance(UserEventsSessionHandler.FirstReconnectDelay);
        var beforeSecondDelay = attempts;
        time.Advance(UserEventsSessionHandler.FirstReconnectDelay);
        await WaitForAsync(() => attempts == 3);

        // Assert
        Assert.Equal(2, beforeSecondDelay);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GivenConnected_WhenTheConnectionDrops_ThenItReconnectsLater()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);

        // Act
        state = UserEventConnectionState.Disconnected;
        mockChannel.Raise(x => x.StateChanged += null, EventArgs.Empty);
        time.Advance(UserEventsSessionHandler.FirstReconnectDelay);
        await WaitForAsync(() => state == UserEventConnectionState.Connected);

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(session), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenDisconnected_WhenTheSessionResumes_ThenItReconnectsAtOnce()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        state = UserEventConnectionState.Disconnected;

        // Act
        mockMonitor.Raise(x => x.SessionResumed += null, EventArgs.Empty);
        await WaitForAsync(() => state == UserEventConnectionState.Connected);

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(session), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenSignedOut_WhenReconnectAsync_ThenNothingConnects()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        await sut.OnSigningOutAsync();

        // Act
        await sut.ReconnectAsync();

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenConnected_WhenOfflineModeTurnsOn_ThenDisconnects()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);
        await WaitForAsync(() => state == UserEventConnectionState.Disconnected);

        // Assert
        mockChannel.Verify(x => x.DisconnectAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenSignedOutElsewhere_WhenReconnectAsync_ThenNothingConnects()
    {
        // Arrange: the handler is still active, but the session has gone.
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        state = UserEventConnectionState.Disconnected;
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        // Act
        await sut.ReconnectAsync();

        // Assert
        mockAuth.Verify(x => x.GetCurrentUserAsync(), Times.Once);
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenTheSessionIsNowAnOfflineOne_WhenReconnectAsync_ThenNothingConnects()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        state = UserEventConnectionState.Disconnected;
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IsOffline = true });

        // Act
        await sut.ReconnectAsync();

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenAConnectInProgress_WhenReconnectAsync_ThenNoSecondConnectIsStarted()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        mockChannel.Setup(x => x.ConnectAsync(It.IsAny<UserSession>())).Returns(pending.Task);
        using var sut = CreateSut();
        var started = sut.OnSessionStartedAsync(session);

        // Act
        await sut.ReconnectAsync();
        pending.SetResult();
        await started;

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenSignedOutWhileConnecting_WhenTheConnectFails_ThenNoReconnectIsScheduled()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        mockChannel.Setup(x => x.ConnectAsync(It.IsAny<UserSession>())).Returns(pending.Task);
        using var sut = CreateSut();
        var started = sut.OnSessionStartedAsync(session);
        await sut.OnSigningOutAsync();

        // Act
        pending.SetException(new HttpRequestException("offline"));
        await started;
        time.Advance(UserEventsSessionHandler.MaxReconnectDelay);
        await Task.Delay(50);

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenGettingTheSessionFails_WhenTheSessionResumes_ThenTheErrorIsSwallowedAndNothingConnects()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        state = UserEventConnectionState.Disconnected;
        var asked = new TaskCompletionSource();
        mockAuth.Setup(x => x.GetCurrentUserAsync()).Callback(() => asked.TrySetResult()).ThrowsAsync(new InvalidOperationException("storage unavailable"));

        // Act
        var exception = Record.Exception(() => mockMonitor.Raise(x => x.SessionResumed += null, EventArgs.Empty));
        await asked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Null(exception);
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenEndingTheSessionFails_WhenSignedOutEverywhereArrives_ThenTheErrorIsSwallowedAndEventsAreNoLongerHandled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        mockAuth.Setup(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var signedOut = new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = "other" };

        // Act
        var first = Record.Exception(() => mockChannel.Raise(x => x.Received += null, mockChannel.Object, signedOut));
        var second = Record.Exception(() => mockChannel.Raise(x => x.Received += null, mockChannel.Object, signedOut));

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        mockAuth.Verify(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task GivenDisconnectingFails_WhenOfflineModeTurnsOn_ThenTheErrorIsSwallowedAndEventsAreNoLongerHandled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        mockChannel.Setup(x => x.DisconnectAsync()).ThrowsAsync(new InvalidOperationException("socket gone"));

        // Act
        var exception = Record.Exception(() => offlineMode.Enter(OfflineEntryReason.Chosen));
        mockChannel.Raise(x => x.Received += null, mockChannel.Object, new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = "other" });

        // Assert
        Assert.Null(exception);
        mockChannel.Verify(x => x.DisconnectAsync(), Times.Once);
        mockAuth.Verify(x => x.EndSessionAsync(It.IsAny<SessionEndedReason>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenOnlineSessionWhileOfflineModeIsOn_WhenSessionStarts_ThenNothingConnects()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        using var sut = CreateSut();

        // Act
        await sut.OnSessionStartedAsync(session);
        await sut.ReconnectAsync();

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenAReconnectAlreadyScheduled_WhenTheConnectionDropsAgain_ThenTheScheduleIsKept()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);
        state = UserEventConnectionState.Disconnected;
        mockChannel.Raise(x => x.StateChanged += null, EventArgs.Empty);

        // Act
        mockChannel.Raise(x => x.StateChanged += null, EventArgs.Empty);
        time.Advance(UserEventsSessionHandler.FirstReconnectDelay);
        await WaitForAsync(() => state == UserEventConnectionState.Connected);

        // Assert: had the second drop rescheduled, the wait would have doubled.
        Assert.Equal(UserEventConnectionState.Connected, state);
        mockChannel.Verify(x => x.ConnectAsync(session), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenConnected_WhenTheStateChangesWithoutDropping_ThenNoReconnectIsScheduled()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnSessionStartedAsync(session);

        // Act
        mockChannel.Raise(x => x.StateChanged += null, EventArgs.Empty);
        time.Advance(UserEventsSessionHandler.MaxReconnectDelay);
        await Task.Delay(50);

        // Assert
        mockChannel.Verify(x => x.ConnectAsync(session), Times.Once);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    private UserEventsSessionHandler CreateSut() => new(mockChannel.Object, mockAuth.Object, mockMonitor.Object, offlineMode, mockDeviceName.Object, time);
}
