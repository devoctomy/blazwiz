using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

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
        // Act & Assert
        Assert.Equal("folderService", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(null!, mockScheduler.Object, mockBrowser.Object, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, null!, mockBrowser.Object, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("browserNetworkStatus", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, null!, mockShellContent.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("shellContentService", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, mockBrowser.Object, null!, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, mockBrowser.Object, mockShellContent.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new SyncFolderAccessMonitor(mockFolder.Object, mockScheduler.Object, mockBrowser.Object, mockShellContent.Object, testText.Sync, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheFolderStatusIsGone_WhenReconnectAsync_ThenSaysTheGrantWasNotKept()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.ReconnectAsync()).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.CheckAsync();
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.True(sut.NeedsAccess);
        Assert.Null(sut.FolderName);
        Assert.StartsWith("The browser did not keep access to the folder.", sut.Error);
        Assert.Equal(1, changes);
        mockShellContent.Verify(x => x.RequestReload(), Times.Never);
        mockScheduler.Verify(x => x.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSyncFailsToStart_WhenReconnectAsync_ThenTheFolderIsStillReconnected()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.ReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "granted" });
        mockScheduler.Setup(x => x.StartAsync()).ThrowsAsync(new InvalidOperationException("offline"));
        using var sut = CreateSut();
        await sut.CheckAsync();
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        var exception = await Record.ExceptionAsync(() => sut.ReconnectAsync());

        // Assert
        Assert.Null(exception);
        Assert.False(sut.NeedsAccess);
        Assert.Null(sut.Error);
        Assert.Equal(1, changes);
        mockShellContent.Verify(x => x.RequestReload(), Times.Once);
    }

    [Fact]
    public async Task GivenAnEarlierError_WhenReconnectSucceeds_ThenTheErrorIsCleared()
    {
        // Arrange
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.SetupSequence(x => x.ReconnectAsync()).ReturnsAsync(false).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "granted" });
        using var sut = CreateSut();
        await sut.CheckAsync();
        await sut.ReconnectAsync();
        var error = sut.Error;

        // Act
        await sut.ReconnectAsync();

        // Assert
        Assert.Equal("Access to the folder was not allowed.", error);
        Assert.Null(sut.Error);
        Assert.False(sut.NeedsAccess);
    }

    [Fact]
    public void GivenStoppingSyncFails_WhenAppBecomesVisibleWithAccessLost_ThenNothingThrowsAndAccessIsAskedFor()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();
        mockFolder.Setup(x => x.NeedsReconnectAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.GetStatusAsync()).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = "prompt" });
        mockScheduler.Setup(x => x.StopAsync()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var exception = Record.Exception(() => mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty));

        // Assert
        Assert.Null(exception);
        Assert.True(sut.NeedsAccess);
        mockScheduler.Verify(x => x.StopAsync(), Times.Once);
    }

    [Fact]
    public void GivenStartedTwice_WhenAppBecomesVisible_ThenCheckedOnce()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();
        sut.Start();

        // Act
        mockBrowser.Raise(x => x.BecameVisible += null, EventArgs.Empty);

        // Assert
        mockFolder.Verify(x => x.NeedsReconnectAsync(), Times.Once);
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
