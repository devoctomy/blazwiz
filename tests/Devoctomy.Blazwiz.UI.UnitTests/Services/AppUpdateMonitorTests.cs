using Devoctomy.Blazwiz.UI.Services;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class AppUpdateMonitorTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public AppUpdateMonitorTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == AppUpdateMonitor.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new AppUpdateMonitor(null!)).ParamName);
    }

    [Fact]
    public async Task GivenNew_WhenRead_ThenNotDownloading()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);

        // Assert
        Assert.False(sut.IsDownloading);
        Assert.Null(sut.Progress);
    }

    [Fact]
    public async Task GivenStarted_WhenStartAsyncAgain_ThenWatchedOnce()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("watch", It.Is<object?[]>(a => a[0] is DotNetObjectReference<AppUpdateMonitor>)), Times.Once);
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenWatching_WhenDownloadingWithProgress_ThenDownloadingWithTheFractionDone_AndChangedRaised()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);
        var changed = 0;
        sut.Changed += (_, _) => changed++;

        // Act
        sut.OnDownloading(3, 12);

        // Assert
        Assert.True(sut.IsDownloading);
        Assert.Equal(0.25, sut.Progress);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task GivenWatching_WhenDownloadingWithoutATotal_ThenDownloadingWithNoProgress()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);

        // Act
        sut.OnDownloading(0, 0);

        // Assert
        Assert.True(sut.IsDownloading);
        Assert.Null(sut.Progress);
    }

    [Fact]
    public async Task GivenMoreDoneThanTotal_WhenDownloading_ThenProgressIsCapped()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);

        // Act
        sut.OnDownloading(15, 12);

        // Assert
        Assert.Equal(1, sut.Progress);
    }

    [Fact]
    public async Task GivenDownloading_WhenFinished_ThenNotDownloading_AndChangedRaised()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);
        sut.OnDownloading(3, 12);
        var changed = 0;
        sut.Changed += (_, _) => changed++;

        // Act
        sut.OnFinished();

        // Assert
        Assert.False(sut.IsDownloading);
        Assert.Null(sut.Progress);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task GivenNotDownloading_WhenFinished_ThenNothingRaised()
    {
        // Arrange
        await using var sut = new AppUpdateMonitor(mockJs.Object);
        var changed = 0;
        sut.Changed += (_, _) => changed++;

        // Act
        sut.OnFinished();

        // Assert
        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task GivenStarted_WhenDisposed_ThenModuleDisposed()
    {
        // Arrange
        var sut = new AppUpdateMonitor(mockJs.Object);
        await sut.StartAsync();

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenThePageGoingAway_WhenDisposed_ThenNoThrow()
    {
        // Arrange
        mockModule.Setup(x => x.DisposeAsync()).ThrowsAsync(new JSDisconnectedException("gone"));
        var sut = new AppUpdateMonitor(mockJs.Object);
        await sut.StartAsync();

        // Act & Assert
        await sut.DisposeAsync();
    }
}
