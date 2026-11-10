using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Sessions;

public class SessionResumeMonitorTests
{
    private readonly Mock<IPageResumeService> mockPageResume = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("pageResumeService", Assert.Throws<ArgumentNullException>(() => new SessionResumeMonitor(null!, mockAuth.Object)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SessionResumeMonitor(mockPageResume.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenStarted_WhenStartAsyncAgain_ThenTheBrowserIsWatchedOnce()
    {
        // Arrange
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);

        // Act
        await sut.StartAsync();
        await sut.StartAsync();

        // Assert
        mockPageResume.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenStartedAndTheSessionIsGood_WhenTheAppResumes_ThenTheSessionIsChecked_AndSessionResumedRaised()
    {
        // Arrange
        mockAuth.Setup(x => x.CheckSessionAsync()).ReturnsAsync(true);
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);
        var resumed = new TaskCompletionSource();
        sut.SessionResumed += (_, _) => resumed.TrySetResult();
        await sut.StartAsync();

        // Act
        mockPageResume.Raise(x => x.Resumed += null, EventArgs.Empty);

        // Assert
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        mockAuth.Verify(x => x.CheckSessionAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenTheSessionWasRevoked_WhenCheckAsync_ThenSessionResumedNotRaised()
    {
        // Arrange
        mockAuth.Setup(x => x.CheckSessionAsync()).ReturnsAsync(false);
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);
        var resumed = 0;
        sut.SessionResumed += (_, _) => resumed++;

        // Act
        await sut.CheckAsync();

        // Assert
        Assert.Equal(0, resumed);
    }

    [Fact]
    public async Task GivenACheckRunning_WhenCheckAsyncAgain_ThenTheSessionIsCheckedOnce()
    {
        // Arrange
        var check = new TaskCompletionSource<bool>();
        mockAuth.Setup(x => x.CheckSessionAsync()).Returns(check.Task);
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);
        var first = sut.CheckAsync();

        // Act
        await sut.CheckAsync();
        check.SetResult(true);
        await first;

        // Assert
        mockAuth.Verify(x => x.CheckSessionAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenStopped_WhenTheAppResumes_ThenNothingIsChecked()
    {
        // Arrange
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);
        await sut.StartAsync();

        // Act
        await sut.StopAsync();
        await sut.StopAsync();
        mockPageResume.Raise(x => x.Resumed += null, EventArgs.Empty);

        // Assert
        mockPageResume.Verify(x => x.StopAsync(), Times.Once);
        mockAuth.Verify(x => x.CheckSessionAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheCheckThrows_WhenTheAppResumes_ThenNothingEscapes()
    {
        // Arrange
        mockAuth.Setup(x => x.CheckSessionAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        using var sut = new SessionResumeMonitor(mockPageResume.Object, mockAuth.Object);
        await sut.StartAsync();

        // Act
        var exception = Record.Exception(() => mockPageResume.Raise(x => x.Resumed += null, EventArgs.Empty));

        // Assert
        Assert.Null(exception);
    }
}
