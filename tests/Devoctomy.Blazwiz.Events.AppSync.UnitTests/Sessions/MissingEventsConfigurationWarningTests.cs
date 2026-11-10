using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Events.AppSync.Sessions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Devoctomy.Blazwiz.Events.AppSync.UnitTests.Sessions;

public class MissingEventsConfigurationWarningTests
{
    [Fact]
    public void GivenNullLogger_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => new MissingEventsConfigurationWarning(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSessionsStarting_WhenOnSessionStartedAsync_ThenOneWarningIsLogged()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MissingEventsConfigurationWarning>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = new MissingEventsConfigurationWarning(mockLogger.Object);

        // Act
        await sut.OnSessionStartedAsync(new UserSession { Username = "bob" });
        await sut.OnSessionStartedAsync(new UserSession { Username = "bob" });

        // Assert
        mockLogger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
