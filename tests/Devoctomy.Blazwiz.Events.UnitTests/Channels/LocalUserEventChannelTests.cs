using System.Text.Json;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Events.Channels;
using Devoctomy.Blazwiz.Events.Models;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Events.UnitTests.Channels;

public class LocalUserEventChannelTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();
    private readonly Mock<IAppStorageKeys> mockKeys = new();

    public LocalUserEventChannelTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == LocalUserEventChannel.ModulePath)))
            .ReturnsAsync(mockModule.Object);
        mockKeys.Setup(x => x.For(It.IsAny<string>())).Returns((string name) => $"testapp_{name}");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new LocalUserEventChannel(null!, mockKeys.Object)).ParamName);
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new LocalUserEventChannel(mockJs.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSession_WhenConnectAsync_ThenTheUsersChannelIsOpened_AndConnected()
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        var changes = 0;
        sut.StateChanged += (_, _) => changes++;

        // Act
        await sut.ConnectAsync(new UserSession { Username = " Bob@Example.com " });
        await sut.ConnectAsync(new UserSession { Username = "bob@example.com" });

        // Assert
        Assert.Equal(UserEventConnectionState.Connected, sut.State);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("connect", It.Is<object?[]>(a => (string?)a[0] == "testapp_events:bob@example.com" && a[1] is DotNetObjectReference<LocalUserEventChannel>)), Times.Once);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task GivenSession_WhenPublishAsync_ThenTheEventIsPostedAsJsonOnTheUsersChannel_ConnectedOrNot()
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        string? name = null;
        string? posted = null;
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("publish", It.IsAny<object?[]>()))
            .Callback<string, object?[]?>((_, a) => (name, posted) = ((string?)a![0], (string?)a[1]))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await sut.PublishAsync(new UserSession { Username = "Bob" }, new UserEvent { Type = UserEventTypes.SignedOutEverywhere, Reason = UserEventReasons.UserRequested, From = sut.InstanceId });

        // Assert
        Assert.Equal("testapp_events:bob", name);
        using var json = JsonDocument.Parse(posted!);
        Assert.Equal(UserEventTypes.SignedOutEverywhere, json.RootElement.GetProperty("type").GetString());
        Assert.Equal(sut.InstanceId, json.RootElement.GetProperty("from").GetString());
    }

    [Fact]
    public async Task GivenConnected_WhenDisconnectAsync_ThenClosed_AndDisconnected()
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        await sut.ConnectAsync(new UserSession { Username = "bob" });

        // Act
        await sut.DisconnectAsync();
        await sut.DisconnectAsync();

        // Assert
        Assert.Equal(UserEventConnectionState.Disconnected, sut.State);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("disconnect", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnotherInstancesEvent_WhenOnMessage_ThenReceivedRaised()
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        UserEvent? received = null;
        sut.Received += (_, e) => received = e;

        // Act
        sut.OnMessage("""{"type":"signed-out-everywhere","reason":"keys-rotated","device":"Phone","at":"2026-11-10T09:00:00+00:00","from":"other"}""");

        // Assert
        Assert.Equal(UserEventTypes.SignedOutEverywhere, received?.Type);
        Assert.Equal(UserEventReasons.KeysRotated, received?.Reason);
        Assert.Equal("Phone", received?.Device);
        Assert.Equal("other", received?.From);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"type":"","from":"other"}""")]
    public async Task GivenUnreadableMessage_WhenOnMessage_ThenIgnored(string json)
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        var received = 0;
        sut.Received += (_, _) => received++;

        // Act
        sut.OnMessage(json);

        // Assert
        Assert.Equal(0, received);
    }

    [Fact]
    public async Task GivenItsOwnEvent_WhenOnMessage_ThenIgnored()
    {
        // Arrange
        await using var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        var received = 0;
        sut.Received += (_, _) => received++;

        // Act
        sut.OnMessage(JsonSerializer.Serialize(new UserEvent { Type = UserEventTypes.SignedOutEverywhere, From = sut.InstanceId }));

        // Assert
        Assert.Equal(0, received);
    }

    [Fact]
    public async Task GivenPageGoingAway_WhenDisposeAsync_ThenDoesNotThrow()
    {
        // Arrange
        var sut = new LocalUserEventChannel(mockJs.Object, mockKeys.Object);
        await sut.ConnectAsync(new UserSession { Username = "bob" });
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("disconnect", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("gone"));

        // Act
        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }
}
