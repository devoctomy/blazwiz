using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Models;
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
    private readonly OfflineModeService offlineMode = new();
    private readonly InMemoryOfflineSessionStore offlineSessions;
    private DateTimeOffset now = Now;

    public LocalAuthenticationServiceTests()
    {
        mockSystemClock.SetupGet(x => x.UtcNow).Returns(() => now);
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername(It.IsAny<string>())).Returns("Bob Hoskins");
        offlineSessions = new InMemoryOfflineSessionStore(mockSystemClock.Object);
    }

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(null!, mockUserDisplay.Object, offlineMode, offlineSessions, mockSystemClock.Object));
        Assert.Equal("localStorageService", exception.ParamName);
    }

    [Fact]
    public void GivenNullSystemClock_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, offlineSessions, null!));
        Assert.Equal("systemClock", exception.ParamName);
    }

    [Fact]
    public void GivenNullUserDisplayService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, null!, offlineMode, offlineSessions, mockSystemClock.Object));
        Assert.Equal("userDisplayService", exception.ParamName);
    }

    [Fact]
    public void GivenNullOfflineDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, mockUserDisplay.Object, null!, offlineSessions, mockSystemClock.Object)).ParamName);
        Assert.Equal("offlineSessionStore", Assert.Throws<ArgumentNullException>(() => new LocalAuthenticationService(mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, null!, mockSystemClock.Object)).ParamName);
    }

    [Fact]
    public async Task GivenRemembered_WhenSignInOfflineAsync_ThenOfflineSessionInMemoryForFifteenMinutesAndOfflineModeOn()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.SignInUnreachable);

        // Assert
        Assert.True(result.Success);
        var session = await sut.GetCurrentUserAsync();
        Assert.NotNull(session);
        Assert.True(session.IsOffline);
        Assert.Equal("bob@example.com", session.Username);
        Assert.Equal("bob@example.com", session.Email);
        Assert.Equal(Now.Add(OfflineModeService.SessionLength), session.ExpiresAt);
        Assert.Equal(OfflineEntryReason.SignInUnreachable, offlineMode.Reason);
        mockLocalStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<UserSession>()), Times.Never);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(LocalAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenOfflineSessionExpired_WhenGetCurrentUserAsync_ThenEndedAndSessionEndedRaised()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.Chosen);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;
        now = Now.Add(OfflineModeService.SessionLength);

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        Assert.Null(offlineSessions.Session);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenOfflineModeTurnedOff_ThenSessionEndedRaised()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.Chosen);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        offlineMode.Exit();

        // Assert
        Assert.Equal(1, ended);
        Assert.Null(offlineSessions.Session);
    }

    [Fact]
    public async Task GivenSignedIn_WhenGoOfflineAsync_ThenSessionBecomesOfflineForFifteenMinutesAndStoredOneRemoved()
    {
        // Arrange
        SetupStoredSession(new UserSession { Username = "bob", DisplayName = "Bob", ExpiresAt = Now.AddHours(8) });
        using var sut = CreateSut();

        // Act
        await sut.GoOfflineAsync();

        // Assert
        var session = await sut.GetCurrentUserAsync();
        Assert.True(session!.IsOffline);
        Assert.Equal("bob", session.Username);
        Assert.Equal(Now.Add(OfflineModeService.SessionLength), session.ExpiresAt);
        Assert.Equal(OfflineEntryReason.Chosen, offlineMode.Reason);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(LocalAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenGoOfflineAsync_ThenThrowsAndOfflineModeStaysOff()
    {
        using var sut = CreateSut();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GoOfflineAsync());
        Assert.False(offlineMode.IsOffline);
    }

    [Fact]
    public async Task GivenOfflineModeWithoutOfflineSession_WhenGetCurrentUserAsync_ThenNobodyWithoutReadingStorage()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetCurrentUserAsync());
        mockLocalStorage.Verify(x => x.GetObjectAsync<UserSession>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenLogoutAsync_ThenForgottenButOfflineModeKept()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.Chosen);

        // Act
        await sut.LogoutAsync();

        // Assert
        Assert.Null(offlineSessions.Session);
        Assert.True(offlineMode.IsOffline);
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

    private LocalAuthenticationService CreateSut() => new(mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, offlineSessions, mockSystemClock.Object);
}
