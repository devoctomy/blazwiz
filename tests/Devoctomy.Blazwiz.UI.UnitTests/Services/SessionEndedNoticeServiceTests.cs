using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.UI.Services;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class SessionEndedNoticeServiceTests
{
    [Fact]
    public void GivenNothingSet_WhenTake_ThenNull()
    {
        // Arrange
        var sut = new SessionEndedNoticeService();

        // Act
        var result = sut.Take();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GivenSet_WhenTakeTwice_ThenGivenOnce()
    {
        // Arrange
        var sut = new SessionEndedNoticeService();
        var args = new SessionEndedEventArgs(SessionEndedReason.Revoked);
        sut.Set(new SessionEndedEventArgs(SessionEndedReason.Expired));
        sut.Set(args);

        // Act
        var first = sut.Take();
        var second = sut.Take();

        // Assert
        Assert.Same(args, first);
        Assert.Null(second);
    }

    [Fact]
    public void GivenNull_WhenSet_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new SessionEndedNoticeService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Set(null!));
    }
}
