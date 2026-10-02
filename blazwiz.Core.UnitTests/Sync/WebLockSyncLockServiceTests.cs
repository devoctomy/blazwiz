using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Connectivity;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Core.UnitTests.Sync;

public class WebLockSyncLockServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public WebLockSyncLockServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == BrowserNetworkStatus.ModulePath)))
            .ReturnsAsync(mockModule.Object);
        mockModule.Setup(x => x.InvokeAsync<int>("acquireLock", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == "blazwiz-sync-notes")))
            .ReturnsAsync(7);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new WebLockSyncLockService(null!)).ParamName);
    }

    [Fact]
    public async Task GivenLockName_WhenAcquiredAndDisposed_ThenWebLockTakenAndReleasedOnce()
    {
        // Arrange
        var sut = new WebLockSyncLockService(mockJs.Object);

        // Act
        var handle = await sut.AcquireAsync("blazwiz-sync-notes");
        await handle.DisposeAsync();
        await handle.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("releaseLock", It.Is<object?[]>(a => (int)a[0]! == 7)), Times.Once);
    }

    [Fact]
    public async Task GivenPageGone_WhenReleasing_ThenNoException()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("releaseLock", It.IsAny<object?[]>())).ThrowsAsync(new JSDisconnectedException("gone"));
        var handle = await new WebLockSyncLockService(mockJs.Object).AcquireAsync("blazwiz-sync-notes");

        // Act & Assert
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task GivenNoName_WhenAcquireAsync_ThenThrows()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WebLockSyncLockService(mockJs.Object).AcquireAsync(" "));
    }
}
