using Devoctomy.Blazwiz.Browser.Services;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class PageResumeServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public PageResumeServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == PageResumeService.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new PageResumeService(null!)).ParamName);
    }

    [Fact]
    public async Task GivenStarted_WhenStartAsyncAgain_ThenSubscribedOnce()
    {
        // Arrange
        await using var sut = new PageResumeService(mockJs.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("subscribe", It.Is<object?[]>(a => a[0] is DotNetObjectReference<PageResumeService>)), Times.Once);
    }

    [Fact]
    public async Task GivenStarted_WhenStopAsync_ThenUnsubscribedOnce()
    {
        // Arrange
        await using var sut = new PageResumeService(mockJs.Object);
        await sut.StartAsync();

        // Act
        await sut.StopAsync();
        await sut.StopAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenListener_WhenOnResumed_ThenResumedRaised()
    {
        // Arrange
        await using var sut = new PageResumeService(mockJs.Object);
        var raised = 0;
        sut.Resumed += (_, _) => raised++;

        // Act
        sut.OnResumed();

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenPageGoingAway_WhenDisposeAsync_ThenDoesNotThrow()
    {
        // Arrange
        var sut = new PageResumeService(mockJs.Object);
        await sut.StartAsync();
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("unsubscribe", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("gone"));

        // Act
        var exception = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }
}
