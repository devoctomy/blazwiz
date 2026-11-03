using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Shell.Interfaces;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class SyncFolderAccessMonitorTests
{
    private readonly TestText testText = new();

    private readonly Mock<ISyncFolderService> mockFolder = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<IBrowserNetworkStatus> mockBrowser = new();
    private readonly Mock<IShellContentService> mockShellContent = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("folderService", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(null!, mockScheduler.Object, mockBrowser.Object, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, null!, mockBrowser.Object, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("browserNetworkStatus", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, null!, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("shellContentService", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, mockBrowser.Object, null!, testText.Sync, testText.Errors)).ParamName);
    }

    [Fact]
    public async Task GivenFolderNeedsPermission_WhenCheckAsync_ThenNeedsAccessWithNameAndChangeRaised()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "prompt" });
        using var sut = CreateSut();
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        var needed = await sut.CheckAsync();

        // Assert
        Assert.True(needed);
        Assert.True(sut.NeedsAccess);
        Assert.Equal("Notes", sut.FolderName);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task GivenFolderCheckFails_WhenCheckAsync_ThenAccessNotNeeded()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        using var sut = CreateSut();

        // Act & Assert
        Assert.False(await sut.CheckAsync());
        Assert.False(sut.NeedsAccess);
    }

    [Fact]
    public async Task GivenUserAllows_WhenReconnectAsync_ThenContentShownAgainAndSyncStarts()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.ReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "granted" });
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.False(sut.NeedsAccess);
        Assert.Null(sut.Error);
        mockShellContent.Verify(x => x.RequestReload(), Times.Once);
        mockScheduler.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenUserRefuses_WhenReconnectAsync_ThenStillNeedsAccessAndSaysSo()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.True(sut.NeedsAccess);
        Assert.Equal("Access to the folder was not allowed.", sut.Error);
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenBrowserDoesNotKeepTheGrant_WhenReconnectAsync_ThenSaysSoAndSuggestsBrowserStorage()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.ReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "prompt" });
        using var sut = CreateSut();
        await sut.CheckAsync();

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.True(sut.NeedsAccess);
        Assert.StartsWith("The browser did not keep access to the folder.", sut.Error);
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenReconnectFails_WhenReconnectAsync_ThenShowsWhy()
    {
        // Arrange
        mockFolder.Setup(x => x.ReconnectAsync()).ThrowsAsync(new InvalidOperationException("SecurityError"));
        using var sut = CreateSut();

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.Equal("The folder could not be reconnected: Something went wrong.", sut.Error);
    }

    [Fact]
    public void GivenWatchingAndAccessLostInTheBackground_WhenAppBecomesVisible_ThenNeedsAccessAndSyncStoppedOnce()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "prompt" });

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);

        // Assert
        Assert.True(sut.NeedsAccess);
        Assert.Equal("Notes", sut.FolderName);
        mockScheduler.Verify(x => x.StopAsync(), Times.Once);
    }

    [Fact]
    public void GivenWatchingAndAccessKept_WhenAppBecomesVisible_ThenNothingChanges()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);

        // Assert
        Assert.False(sut.NeedsAccess);
        mockScheduler.Verify(x => x.StopAsync(), Times.Never);
    }

    [Fact]
    public void GivenNotWatchingOrStopped_WhenAppBecomesVisible_ThenNotChecked()
    {
        // Arrange
        using var sut = CreateSut();
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);
        sut.Start();
        sut.Stop();

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);

        // Assert
        mockFolder.Verify(x => x.NeedsReconnectAsync(), Times.Never);
    }

    [Fact]
    public void GivenDisposed_WhenAppBecomesVisible_ThenNotChecked()
    {
        // Arrange
        var sut = CreateSut();
        sut.Start();

        // Act
        sut.Dispose();
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);

        // Assert
        mockFolder.Verify(x => x.NeedsReconnectAsync(), Times.Never);
    }

    private SyncFolderAccessMonitor CreateSut() => new(mockFolder.Object, mockScheduler.Object, mockBrowser.Object, mockShellContent.Object, testText.Sync, testText.Errors);
}
