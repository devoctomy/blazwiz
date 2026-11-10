using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Sessions;

public class SessionResumeSessionHandlerTests
{
    [Fact]
    public void GivenNullMonitor_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("monitor", Assert.Throws<ArgumentNullException>(() => new SessionResumeSessionHandler(null!)).ParamName);
    }

    [Fact]
    public async Task GivenHandler_WhenTheSessionStartsAndEnds_ThenTheMonitorStartsAndStops()
    {
        // Arrange
        var monitor = new Mock<ISessionResumeMonitor>();
        var sut = new SessionResumeSessionHandler(monitor.Object);

        // Act
        await sut.OnSessionStartedAsync(new UserSession { Username = "bob" });
        await sut.OnSigningOutAsync();

        // Assert
        monitor.Verify(x => x.StartAsync(), Times.Once);
        monitor.Verify(x => x.StopAsync(), Times.Once);
    }
}
