using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class FolderAccessBannerViewModelTests
{
    private readonly Mock<ISyncFolderAccessMonitor> mockMonitor = new();

    [Fact]
    public void GivenNullMonitor_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("monitor", Assert.Throws<ArgumentNullException>(() => new FolderAccessBannerViewModel(null!)).ParamName);
    }

    [Fact]
    public void GivenAccessNeeded_WhenRead_ThenShownWithFolderAndError()
    {
        // Arrange
        mockMonitor.SetupGet(x => x.NeedsAccess).Returns(true);
        mockMonitor.SetupGet(x => x.FolderName).Returns("Notes");
        mockMonitor.SetupGet(x => x.Error).Returns("Access to the folder was not allowed.");
        var sut = new FolderAccessBannerViewModel(mockMonitor.Object);

        // Act & Assert
        Assert.True(sut.IsVisible);
        Assert.Equal("Notes", sut.FolderName);
        Assert.Equal("Access to the folder was not allowed.", sut.Error);
    }

    [Fact]
    public async Task GivenBanner_WhenReconnect_ThenMonitorReconnects()
    {
        // Arrange
        var sut = new FolderAccessBannerViewModel(mockMonitor.Object);

        // Act
        await sut.ReconnectCommand.ExecuteAsync(null);

        // Assert
        mockMonitor.Verify(x => x.ReconnectAsync(), Times.Once);
    }

    [Fact]
    public void GivenMonitorChanges_WhenRaised_ThenBannerNotified()
    {
        // Arrange
        var sut = new FolderAccessBannerViewModel(mockMonitor.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockMonitor.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(FolderAccessBannerViewModel.IsVisible), changed);
        Assert.Contains(nameof(FolderAccessBannerViewModel.FolderName), changed);
        Assert.Contains(nameof(FolderAccessBannerViewModel.Error), changed);
    }

    [Fact]
    public void GivenDisposed_WhenMonitorChanges_ThenNotNotified()
    {
        // Arrange
        var sut = new FolderAccessBannerViewModel(mockMonitor.Object);
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;

        // Act
        sut.Dispose();
        mockMonitor.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Equal(0, changed);
    }
}
