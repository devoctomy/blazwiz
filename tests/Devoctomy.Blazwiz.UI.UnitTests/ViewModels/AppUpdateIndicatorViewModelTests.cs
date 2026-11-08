using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class AppUpdateIndicatorViewModelTests
{
    private readonly FrameworkText testText = new();

    private readonly Mock<IAppUpdateMonitor> mockMonitor = new();

    [Fact]
    public void GivenNullMonitor_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("monitor", Assert.Throws<ArgumentNullException>(() => new AppUpdateIndicatorViewModel(null!, testText.Ui)).ParamName);
    }

    [Fact]
    public void GivenNullText_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new AppUpdateIndicatorViewModel(mockMonitor.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNew_WhenInitialized_ThenTheMonitorIsStarted()
    {
        // Arrange
        using var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockMonitor.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public void GivenNothingDownloading_WhenRead_ThenHidden()
    {
        // Arrange
        mockMonitor.SetupGet(x => x.IsDownloading).Returns(false);

        // Act
        using var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);

        // Assert
        Assert.False(sut.IsVisible);
    }

    [Fact]
    public void GivenDownloadingWithoutProgress_WhenRead_ThenShownWithoutAPercentage()
    {
        // Arrange
        mockMonitor.SetupGet(x => x.IsDownloading).Returns(true);
        mockMonitor.SetupGet(x => x.Progress).Returns((double?)null);

        // Act
        using var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal("Downloading update…", sut.Message);
    }

    [Fact]
    public void GivenDownloadingWithProgress_WhenRead_ThenShownWithThePercentage()
    {
        // Arrange
        mockMonitor.SetupGet(x => x.IsDownloading).Returns(true);
        mockMonitor.SetupGet(x => x.Progress).Returns(0.256);

        // Act
        using var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);

        // Assert
        Assert.Equal("Downloading update… 26%", sut.Message);
    }

    [Fact]
    public void GivenShown_WhenTheMonitorChanges_ThenVisibilityAndMessageChange()
    {
        // Arrange
        using var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockMonitor.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Equal([nameof(AppUpdateIndicatorViewModel.IsVisible), nameof(AppUpdateIndicatorViewModel.Message)], changed);
    }

    [Fact]
    public void GivenDisposed_WhenTheMonitorChanges_ThenNothingChanges()
    {
        // Arrange
        var sut = new AppUpdateIndicatorViewModel(mockMonitor.Object, testText.Ui);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        sut.Dispose();

        // Act
        mockMonitor.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Empty(changed);
    }
}
