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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    private UserEventsSessionHandler CreateSut() => new(mockChannel.Object, mockAuth.Object, mockMonitor.Object, offlineMode, mockDeviceName.Object, time);
}
