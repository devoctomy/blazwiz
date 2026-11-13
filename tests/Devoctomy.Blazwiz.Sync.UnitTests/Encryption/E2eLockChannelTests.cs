using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class E2eLockChannelTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = new();
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public E2eLockChannelTests()
    {
        mockKeys.Setup(x => x.For(It.IsAny<string>())).Returns<string>(name => "myapp_" + name);
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == SyncStorageChannel.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new E2eLockChannel(null!, mockKeys.Object)).ParamName);
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new E2eLockChannel(mockJs.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenStartedTwice_WhenStartAsync_ThenSubscribesOnceToTheAppsLockChannel()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("subscribe", It.Is<object?[]>(a => (string?)a[0] == "myapp_" + SyncDefaults.LockChannelName && a[1] is DotNetObjectReference<E2eLockChannel>)), Times.Once);
    }

    [Fact]
    public async Task GivenAProfileId_WhenPostLockAsync_ThenPostedOnTheLockChannel()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.PostLockAsync("abc");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("post", It.Is<object?[]>(a => (string?)a[0] == "myapp_" + SyncDefaults.LockChannelName && (string?)a[1] == "abc")), Times.Once);
    }

    [Fact]
    public async Task GivenNoProfileId_WhenPostLockAsync_ThenThrows()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.PostLockAsync(" "));
    }

    [Fact]
    public void GivenAMessage_WhenOnMessage_ThenLockReceivedIsRaised()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);
        var received = new List<string>();
        sut.LockReceived += (_, id) => received.Add(id);

        // Act
        sut.OnMessage("abc");
        sut.OnMessage(" ");

        // Assert
        Assert.Equal(["abc"], received);
    }

    [Fact]
    public async Task GivenStarted_WhenDisposed_ThenUnsubscribesFromTheLockChannel()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);
        await sut.StartAsync();

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.Is<object?[]>(a => (string?)a[0] == "myapp_" + SyncDefaults.LockChannelName)), Times.Once);
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenStartedAndThePageIsGoingAway_WhenDisposed_ThenNothingIsThrown()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()))
            .Returns(ValueTask.FromException<IJSVoidResult>(new JSDisconnectedException("gone")));
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);
        await sut.StartAsync();

        // Act
        var ex = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        // Assert
        Assert.Null(ex);
        mockModule.Verify(x => x.DisposeAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenOnlyPosted_WhenDisposed_ThenTheModuleIsDisposedWithoutUnsubscribing()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);
        await sut.PostLockAsync("abc");

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()), Times.Never);
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenNeverUsed_WhenDisposed_ThenTheModuleIsNeverLoaded()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);

        // Act
        await sut.DisposeAsync();

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public void GivenNoListener_WhenOnMessage_ThenNothingIsThrown()
    {
        // Arrange
        var sut = new E2eLockChannel(mockJs.Object, mockKeys.Object);

        // Act
        var ex = Record.Exception(() => sut.OnMessage("abc"));

        // Assert
        Assert.Null(ex);
    }
}
