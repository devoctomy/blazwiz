using blazwiz.Core.Offline;
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Services;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class InMemoryOfflineSessionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISystemClock> mockClock = new();

    public InMemoryOfflineSessionStoreTests()
    {
        mockClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenNullClock_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new InMemoryOfflineSessionStore(null!)).ParamName);
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
        var sut = new InMemoryOfflineSessionStore(mockClock.Object);
        sut.Start("bob", "Bob", null);
        sut.Clear();
        Assert.Null(sut.Session);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenBlankUsername_WhenStart_ThenThrows(string username)
    {
        Assert.Throws<ArgumentException>(() => new InMemoryOfflineSessionStore(mockClock.Object).Start(username, "Bob", null));
    }
}
