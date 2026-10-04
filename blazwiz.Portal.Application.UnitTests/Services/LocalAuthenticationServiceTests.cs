using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class LocalAuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<ISystemClock> mockSystemClock = new();

    public LocalAuthenticationServiceTests()
    {
        mockSystemClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(null!, mockUserDisplay.Object, mockSystemClock.Object));
        Assert.Equal("localStorageService", exception.ParamName);
    }

    [Fact]
    public void GivenNullSystemClock_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, mockUserDisplay.Object, null!));
        Assert.Equal("systemClock", exception.ParamName);
    }

    [Fact]
    public void GivenNullUserDisplayService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, null!, mockSystemClock.Object));
        Assert.Equal("userDisplayService", exception.ParamName);
    }

    [Fact]
    public void GivenPlaceholder_WhenGettingIsPlaceholder_ThenTrue()
    {
        Assert.True(CreateSut().IsPlaceholder);
    }

    [Fact]
    public async Task GivenPlaceholder_WhenCompleteNewPasswordAsync_ThenFails()
    {
        // Act
        var result = await CreateSut().CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Please sign in first.", result.Error);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("bob", "")]
    [InlineData("   ", "password")]
    [InlineData("bob", "   ")]
    public async Task GivenBlankCredentials_WhenLoginAsync_ThenFailsWithoutStoringSession(string username, string password)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.LoginAsync(username, password);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Please enter your username and password.", result.Error);
        Assert.Null(result.Session);
        mockLocalStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenEmailUsername_WhenLoginAsync_ThenSucceedsAndStoresSession()
    {
        // Arrange
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername("bob.hoskins@example.com")).Returns("Bob Hoskins");
        var sut = CreateSut();

        // Act
        var result = await sut.LoginAsync("  bob.hoskins@example.com ", "password");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        var session = Assert.IsType<UserSession>(result.Session);
        Assert.Equal("bob.hoskins@example.com", session.Username);
        Assert.Equal("Bob Hoskins", session.DisplayName);
        Assert.Equal("bob.hoskins@example.com", session.Email);
        Assert.Equal(Now, session.SignedInAt);
        Assert.Equal(Now.Add(LocalAuthenticationService.SessionDuration), session.ExpiresAt);
        mockLocalStorage.Verify(x => x.SetObjectAsync(LocalAuthenticationService.SessionStorageKey, session), Times.Once);
    }

    [Fact]
    public async Task GivenPlainUsername_WhenLoginAsync_ThenSessionHasNoEmail()
    {
        // Arrange
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername("alice")).Returns("Alice");
        var sut = CreateSut();

        // Act
        var result = await sut.LoginAsync("alice", "password");

        // Assert
        Assert.Null(result.Session!.Email);
        Assert.Equal("Alice", result.Session.DisplayName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenLogoutAsync_ThenSessionIsRemoved()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LogoutAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(LocalAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenNoSession_WhenGetCurrentUserAsync_ThenReturnsNull()
    {
        // Arrange
        SetupStoredSession(null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenValidSession_WhenGetCurrentUserAsync_ThenReturnsSession()
    {
        // Arrange
        var session = new UserSession { Username = "bob", ExpiresAt = Now.AddMinutes(1) };
        SetupStoredSession(session);
        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Same(session, result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GivenExpiredSession_WhenGetCurrentUserAsync_ThenReturnsNullClearsSessionAndRaisesSessionEnded(int minutesUntilExpiry)
    {
        // Arrange
        SetupStoredSession(new UserSession { Username = "bob", ExpiresAt = Now.AddMinutes(minutesUntilExpiry) });
        var sut = CreateSut();
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(LocalAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(1, ended);
    }

    private void SetupStoredSession(UserSession? session)
    {
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<UserSession>(LocalAuthenticationService.SessionStorageKey))
            .ReturnsAsync(session);
    }

    private LocalAuthenticationService CreateSut() => new(mockLocalStorage.Object, mockUserDisplay.Object, mockSystemClock.Object);
}
