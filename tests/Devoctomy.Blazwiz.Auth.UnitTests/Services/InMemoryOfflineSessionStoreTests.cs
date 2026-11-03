using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Core.Offline;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class InMemoryOfflineSessionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<TimeProvider> mockClock = new();

    public InMemoryOfflineSessionStoreTests()
    {
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
    }

    [Fact]
    public void GivenNullClock_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new InMemoryOfflineSessionStore(null!)).ParamName);
    }

    [Fact]
    public void GivenUser_WhenStart_ThenOfflineSessionForFifteenMinutesWithNoTokens()
    {
        // Act
        var sut = new InMemoryOfflineSessionStore(mockClock.Object);
        var session = sut.Start("bob@example.com", "Bob", "bob@example.com");

        // Assert
        Assert.Same(session, sut.Session);
        Assert.True(session.IsOffline);
        Assert.Equal(Now, session.SignedInAt);
        Assert.Equal(Now.Add(OfflineModeService.SessionLength), session.ExpiresAt);
        Assert.Null(session.IdToken);
        Assert.Null(session.AccessToken);
    }

    [Fact]
    public void GivenSession_WhenClear_ThenGone()
    {
        // Arrange
        var sut = new InMemoryOfflineSessionStore(mockClock.Object);
        sut.Start("bob", "Bob", null);

        // Act
        sut.Clear();

        // Assert
        Assert.Null(sut.Session);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenBlankUsername_WhenStart_ThenThrows(string username)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new InMemoryOfflineSessionStore(mockClock.Object).Start(username, "Bob", null));
    }
}
