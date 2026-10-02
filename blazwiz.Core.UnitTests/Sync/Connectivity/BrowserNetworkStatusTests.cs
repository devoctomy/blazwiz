using blazwiz.Core.Sync.Connectivity;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Connectivity;

public class BrowserNetworkStatusTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public BrowserNetworkStatusTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == BrowserNetworkStatus.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new BrowserNetworkStatus(null!)).ParamName);
    }

    [Fact]
    public async Task GivenBrowserOnline_WhenIsOnlineAsync_ThenTrue()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("isOnline", It.IsAny<object?[]>())).ReturnsAsync(true);
        await using var sut = new BrowserNetworkStatus(mockJs.Object);

        // Act & Assert
        Assert.True(await sut.IsOnlineAsync());
    }

    [Fact]
    public async Task GivenStarted_WhenStartAsyncAgain_ThenSubscribedOnce()
    {
        // Arrange
        await using var sut = new BrowserNetworkStatus(mockJs.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("subscribe", It.Is<object?[]>(a => a[0] is DotNetObjectReference<BrowserNetworkStatus>)), Times.Once);
    }

    [Fact]
    public async Task GivenStarted_WhenStopAsync_ThenUnsubscribed()
    {
        // Arrange
        await using var sut = new BrowserNetworkStatus(mockJs.Object);
        await sut.StartAsync();

        // Act
        await sut.StopAsync();
        await sut.StopAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenBrowserEvents_WhenCalledFromJs_ThenEventsRaised()
    {
        // Arrange
        await using var sut = new BrowserNetworkStatus(mockJs.Object);
        bool? online = null;
        var visible = false;
        sut.OnlineChanged += (_, value) => online = value;
        sut.BecameVisible += (_, _) => visible = true;

        // Act
        sut.OnOnlineChanged(false);
        sut.OnBecameVisible();

        // Assert
        Assert.False(online);
        Assert.True(visible);
    }

    [Fact]
    public async Task GivenPageGone_WhenDisposeAsync_ThenNoException()
    {
        // Arrange
        var sut = new BrowserNetworkStatus(mockJs.Object);
        await sut.StartAsync();
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>())).ThrowsAsync(new JSDisconnectedException("gone"));

        // Act & Assert
        await sut.DisposeAsync();
    }
}
