using System.Text.Json;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Events.AppSync.Channels;
using Devoctomy.Blazwiz.Events.AppSync.Models;
using Devoctomy.Blazwiz.Events.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Events.AppSync.UnitTests.Channels;

public class AppSyncUserEventChannelTests
{
    private const string Sub = "5f2c1a7e-1111-2222-3333-444455556666";

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();
    private readonly AppSyncEventsConfig config = new() { HttpEndpoint = "abc.appsync-api.eu-west-2.amazonaws.com", RealtimeEndpoint = "abc.appsync-realtime-api.eu-west-2.amazonaws.com" };
    private readonly string accessToken = new JsonWebTokenHandler().CreateToken($$"""{"sub":"{{Sub}}","client_id":"client-123"}""");

    public AppSyncUserEventChannelTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == AppSyncUserEventChannel.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new AppSyncUserEventChannel(null!, config)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new AppSyncUserEventChannel(mockJs.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenAccessToken_WhenGetChannelPath_ThenTheUsersChannelFromItsSub()
    {
        // Act
        var result = AppSyncUserEventChannel.GetChannelPath(accessToken);

        // Assert
        Assert.Equal($"/users/{Sub}", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public void GivenNoUsableToken_WhenGetChannelPath_ThenNull(string? token)
    {
        // Act
        var result = AppSyncUserEventChannel.GetChannelPath(token);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GivenTokenWithoutSub_WhenGetChannelPath_ThenNull()
    {
        // Arrange
        var token = new JsonWebTokenHandler().CreateToken("""{"client_id":"client-123"}""");

        // Act
        var result = AppSyncUserEventChannel.GetChannelPath(token);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenSession_WhenConnectAsync_ThenTheModuleSubscribesToTheUsersChannel_AndConnected()
    {
        // Arrange
        await using var sut = CreateSut();

        // Act
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });

        // Assert
        Assert.Equal(UserEventConnectionState.Connected, sut.State);
        mockModule.Verify(
            x => x.InvokeAsync<IJSVoidResult>("connect", It.Is<object?[]>(a =>
                (string?)a[0] == config.RealtimeEndpoint &&
                (string?)a[1] == config.HttpEndpoint &&
                (string?)a[2] == accessToken &&
                (string?)a[3] == $"/users/{Sub}" &&
                a[4] is DotNetObjectReference<AppSyncUserEventChannel>)),
            Times.Once);
    }

    [Fact]
    public async Task GivenSessionWithoutAccessToken_WhenConnectAsync_ThenNothingConnects()
    {
        // Arrange
        await using var sut = CreateSut();

        // Act
        await sut.ConnectAsync(new UserSession { Username = "bob" });

        // Assert
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheServiceRefuses_WhenConnectAsync_ThenThrows_AndDisconnected()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("connect", It.IsAny<object?[]>())).ThrowsAsync(new JSException("subscribe_error"));
        await using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<JSException>(() => sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken }));
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
    }

    [Fact]
    public async Task GivenConnected_WhenTheConnectionCloses_ThenDisconnected_AndStateChangedRaised()
    {
        // Arrange
        await using var sut = CreateSut();
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });
        var changes = 0;
        sut.StateChanged += (_, _) => changes++;

        // Act
        sut.OnClosed();

        // Assert
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task GivenConnected_WhenDisconnectAsync_ThenTheModuleCloses()
    {
        // Arrange
        await using var sut = CreateSut();
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });

        // Act
        await sut.DisconnectAsync();
        await sut.DisconnectAsync();

        // Assert
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("disconnect", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenSession_WhenPublishAsync_ThenPublishedToTheUsersChannelWithItsToken()
    {
        // Arrange
        await using var sut = CreateSut();
        object?[]? args = null;
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("publish", It.IsAny<object?[]>()))
            .Callback<string, object?[]?>((_, a) => args = a)
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await sut.PublishAsync(new UserSession { Username = "bob", AccessToken = accessToken }, new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = sut.InstanceId });

        // Assert
        Assert.Equal(config.HttpEndpoint, args?[0]);
        Assert.Equal(accessToken, args?[1]);
        Assert.Equal($"/users/{Sub}", args?[2]);
        using var json = JsonDocument.Parse((string)args![3]!);
        Assert.Equal(UserEventTypes.SignedOutEverywhere, json.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GivenSessionWithoutAccessToken_WhenPublishAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        await using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PublishAsync(new UserSession { Username = "bob" }, new UserEvent { Type = "x" }));
    }

    [Fact]
    public async Task GivenAnotherInstancesEvent_WhenOnMessage_ThenReceivedRaised_ButNotForItsOwn()
    {
        // Arrange
        await using var sut = CreateSut();
        var received = new List<UserEvent>();
        sut.Received += (_, e) => received.Add(e);

        // Act
        sut.OnMessage(JsonSerializer.Serialize(new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = "other" }));
        sut.OnMessage(JsonSerializer.Serialize(new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = sut.InstanceId }));
        sut.OnMessage("not json");

        // Assert
        Assert.Equal("other", Assert.Single(received).From);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"type":"","from":"other"}""")]
    [InlineData("""{"from":"other"}""")]
    public async Task GivenAnEventWithNoType_WhenOnMessage_ThenIgnored(string json)
    {
        // Arrange
        await using var sut = CreateSut();
        var received = 0;
        sut.Received += (_, _) => received++;

        // Act
        sut.OnMessage(json);

        // Assert
        Assert.Equal(0, received);
    }

    [Fact]
    public async Task GivenNoSubscribers_WhenOnMessage_ThenDoesNotThrow()
    {
        // Arrange
        await using var sut = CreateSut();

        // Act
        var exception = Record.Exception(() => sut.OnMessage(JsonSerializer.Serialize(new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = "other" })));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenPageGoingAway_WhenDisposeAsync_ThenDoesNotThrow()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("disconnect", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("gone"));

        // Act
        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
        mockModule.Verify(x => x.DisposeAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenConnected_WhenDisposeAsync_ThenDisconnectedAndTheModuleDisposed()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ConnectAsync(new UserSession { Username = "bob", AccessToken = accessToken });

        // Act
        await sut.DisposeAsync();

        // Assert
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("disconnect", It.IsAny<object?[]>()), Times.Once);
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private AppSyncUserEventChannel CreateSut() => new(mockJs.Object, config);
}
