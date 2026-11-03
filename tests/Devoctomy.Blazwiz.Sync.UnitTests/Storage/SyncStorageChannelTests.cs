using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Storage;

public class SyncStorageChannelTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public SyncStorageChannelTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == SyncStorageChannel.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new SyncStorageChannel(null!, mockKeys.Object)).ParamName);
    }

    [Fact]
    public async Task GivenStartedTwice_WhenStartAsync_ThenSubscribesOnceToTheStorageChannel()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("subscribe", It.Is<object?[]>(a => (string?)a[0] == SyncDefaults.StorageChannelName && a[1] is DotNetObjectReference<SyncStorageChannel>)), Times.Once);
    }

    [Fact]
    public async Task GivenStarted_WhenStopAsync_ThenUnsubscribesOnce()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);
        await sut.StartAsync();

        // Act
        await sut.StopAsync();
        await sut.StopAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenMessage_WhenPostAsync_ThenPostedByName()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.PostAsync(SyncStorageChannelMessage.Changed);

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("post", It.Is<object?[]>(a => (string?)a[0] == SyncDefaults.StorageChannelName && (string?)a[1] == "Changed")), Times.Once);
    }

    [Theory]
    [InlineData("Migrating", SyncStorageChannelMessage.Migrating)]
    [InlineData("Changed", SyncStorageChannelMessage.Changed)]
    public void GivenKnownMessage_WhenOnMessage_ThenRaised(string message, SyncStorageChannelMessage expected)
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);
        var received = new List<SyncStorageChannelMessage>();
        sut.MessageReceived += (_, m) => received.Add(m);

        // Act
        sut.OnMessage(message);

        // Assert
        Assert.Equal([expected], received);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("7")]
    public void GivenUnknownMessage_WhenOnMessage_ThenIgnored(string message)
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);
        var received = 0;
        sut.MessageReceived += (_, _) => received++;

        // Act
        sut.OnMessage(message);

        // Assert
        Assert.Equal(0, received);
    }

    [Fact]
    public async Task GivenStarted_WhenDisposeAsync_ThenUnsubscribedAndModuleDisposed()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);
        await sut.StartAsync();

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()), Times.Once);
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenPageGone_WhenDisposeAsync_ThenDisconnectIgnored()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>())).ThrowsAsync(new JSDisconnectedException("gone"));
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);
        await sut.StartAsync();

        // Act & Assert
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task GivenNothingStarted_WhenDisposeAsync_ThenNothingImported()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.DisposeAsync();

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }


    [Fact]
    public async Task GivenAppName_WhenStartAndPost_ThenChannelIsNamedAfterTheApp()
    {
        // Arrange
        var sut = new SyncStorageChannel(mockJs.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await sut.StartAsync();
        await sut.PostAsync(SyncStorageChannelMessage.Migrating);

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("subscribe", It.Is<object?[]>(a => (string?)a[0] == "app_sync-storage")), Times.Once);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("post", It.Is<object?[]>(a => (string?)a[0] == "app_sync-storage")), Times.Once);
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new SyncStorageChannel(mockJs.Object, null!)).ParamName);
    }

    // Keeps names as they are, so the tests can check them; the prefix itself is checked separately.
    private static Mock<IAppStorageKeys> CreateKeys()
    {
        var keys = new Mock<IAppStorageKeys>();
        keys.Setup(x => x.For(It.IsAny<string>())).Returns<string>(name => name);
        keys.SetupGet(x => x.Prefix).Returns(string.Empty);
        return keys;
    }
}
