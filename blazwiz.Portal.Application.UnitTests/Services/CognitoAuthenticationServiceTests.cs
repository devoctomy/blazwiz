//#if (IncludeNotes)
using blazwiz.Core.Sync.Interfaces;
//#endif
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Aws;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class CognitoAuthenticationServiceTests
{
    private const string Password = "CorrectHorse123";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 13, 10, 59, TimeSpan.Zero);

    private readonly Mock<ICognitoUserPoolClient> mockCognito = new(MockBehavior.Strict);
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<ISystemClock> mockSystemClock = new();
//#if (IncludeNotes)
    private readonly Mock<IOfflineModeStore> mockOfflineMode = new();
//#endif
    private readonly string idToken = CreateJwt("""{"email":"bob.hoskins@example.com","cognito:username":"5f2c1a7e"}""");

    public CognitoAuthenticationServiceTests()
    {
        mockSystemClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("cognitoClient", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(null!, mockLocalStorage.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockOfflineMode.Object,
//#endif
            mockSystemClock.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, null!, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockOfflineMode.Object,
//#endif
            mockSystemClock.Object)).ParamName);
        Assert.Equal("userDisplayService", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, null!,
//#if (IncludeNotes)
            mockOfflineMode.Object,
//#endif
            mockSystemClock.Object)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
            mockOfflineMode.Object,
//#endif
            null!)).ParamName);
    }

    [Fact]
    public void GivenService_WhenGettingIsPlaceholder_ThenFalse()
    {
        Assert.False(CreateSut().IsPlaceholder);
    }

    [Theory]
    [InlineData("eu-west-2", "pool", "client", true)]
    [InlineData("", "pool", "client", false)]
    [InlineData("eu-west-2", " ", "client", false)]
    [InlineData("eu-west-2", "pool", "", false)]
    public void GivenConfig_WhenIsConfigured_ThenRequiresRegionPoolAndClient(string region, string pool, string client, bool expected)
    {
        Assert.Equal(expected, CognitoAuthenticationService.IsConfigured(new AwsCognitoConfig { Region = region, UserPoolId = pool, UserPoolClientId = client }));
    }

    [Fact]
    public void GivenNullConfig_WhenIsConfigured_ThenFalse()
    {
        Assert.False(CognitoAuthenticationService.IsConfigured(null));
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("bob", " ")]
    public async Task GivenBlankCredentials_WhenLoginAsync_ThenFailsWithoutCallingCognito(string username, string password)
    {
        // Act
        var result = await CreateSut().LoginAsync(username, password);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Please enter your email and password.", result.Error);
        mockCognito.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenCorrectPassword_WhenLoginAsync_ThenStoresSessionFromTokens()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync("bob.hoskins@example.com", Password)).ReturnsAsync(Tokens(idToken));
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername("bob.hoskins@example.com")).Returns("Bob Hoskins");
        var sut = CreateSut();

        // Act
        var result = await sut.LoginAsync(" bob.hoskins@example.com ", Password);

        // Assert
        Assert.True(result.Success, result.Error);
        var session = Assert.IsType<UserSession>(result.Session);
        Assert.Equal("bob.hoskins@example.com", session.Username);
        Assert.Equal("bob.hoskins@example.com", session.Email);
        Assert.Equal("Bob Hoskins", session.DisplayName);
        Assert.Equal(idToken, session.IdToken);
        Assert.Equal("access", session.AccessToken);
        Assert.Equal("refresh", session.RefreshToken);
        Assert.Equal(Now, session.SignedInAt);
        Assert.Equal(Now.AddHours(1), session.ExpiresAt);
        mockLocalStorage.Verify(x => x.SetObjectAsync(CognitoAuthenticationService.SessionStorageKey, session), Times.Once);
    }

    [Fact]
    public async Task GivenNameClaim_WhenLoginAsync_ThenUsesItAsDisplayName()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Tokens(CreateJwt("""{"email":"bob@example.com","name":"Robert H"}""")));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.Equal("Robert H", result.Session!.DisplayName);
        mockUserDisplay.Verify(x => x.GetDisplayNameFromUsername(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenTokenWithoutEmail_WhenLoginAsync_ThenFallsBackToEnteredUsername()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Tokens("not-a-jwt"));
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername("alice@example.com")).Returns("Alice");

        // Act
        var result = await CreateSut().LoginAsync("alice@example.com", Password);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("alice@example.com", result.Session!.Username);
        Assert.Null(result.Session.Email);
        Assert.Equal("Alice", result.Session.DisplayName);
    }

    [Theory]
    [InlineData("NotAuthorizedException", "Incorrect username or password.", "Incorrect username or password.")]
    [InlineData("NotAuthorizedException", "Password attempts exceeded", "Password attempts exceeded")]
    [InlineData("PasswordResetRequiredException", "x", "Your password must be reset before you can sign in. Please contact an administrator.")]
    [InlineData("UserNotConfirmedException", "x", "Your account has not been confirmed yet.")]
    [InlineData("TooManyRequestsException", "x", "Too many attempts. Please wait a moment and try again.")]
    [InlineData("LimitExceededException", "x", "Too many attempts. Please wait a moment and try again.")]
    public async Task GivenCognitoError_WhenLoginAsync_ThenReturnsFriendlyError(string errorType, string message, string expected)
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new CognitoException(errorType, message));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
        mockLocalStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenNetworkFailure_WhenLoginAsync_ThenReturnsConnectionError()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.Equal("Could not reach the sign-in service. Check your connection and try again.", result.Error);
    }

    [Fact]
    public async Task GivenUnsupportedChallenge_WhenLoginAsync_ThenFailsNamingTheChallenge()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CognitoSignInResult { ChallengeName = "SOFTWARE_TOKEN_MFA", SessionId = "s2" });

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.NewPasswordRequired);
        Assert.Equal("Sign-in needs 'SOFTWARE_TOKEN_MFA', which this portal does not support yet.", result.Error);
    }

    [Fact]
    public async Task GivenTemporaryPassword_WhenLoginThenCompleteNewPassword_ThenSignsIn()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito.Setup(x => x.CompleteNewPasswordAsync("s2", "NewPassword123")).ReturnsAsync(Tokens(idToken));

        // Act
        var result = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("bob.hoskins@example.com", result.Session!.Email);
        mockLocalStorage.Verify(x => x.SetObjectAsync(CognitoAuthenticationService.SessionStorageKey, It.IsAny<UserSession>()), Times.Once);

        // The challenge is used up.
        Assert.Equal("Please sign in again.", (await sut.CompleteNewPasswordAsync("NewPassword123")).Error);
    }

    [Fact]
    public async Task GivenNoPendingChallenge_WhenCompleteNewPasswordAsync_ThenAsksToSignInAgain()
    {
        // Act
        var result = await CreateSut().CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal("Please sign in again.", result.Error);
    }

    [Fact]
    public async Task GivenPendingChallenge_WhenCompleteNewPasswordWithBlank_ThenAsksForPasswordAndKeepsChallenge()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();

        // Act
        var result = await sut.CompleteNewPasswordAsync(" ");

        // Assert
        Assert.Equal("Please enter a new password.", result.Error);
        mockCognito.Setup(x => x.CompleteNewPasswordAsync("s2", "NewPassword123")).ReturnsAsync(Tokens(idToken));
        Assert.True((await sut.CompleteNewPasswordAsync("NewPassword123")).Success);
    }

    [Fact]
    public async Task GivenPasswordRejectedByPolicy_WhenCompleteNewPasswordAsync_ThenShowsReasonAndAllowsRetry()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito
            .SetupSequence(x => x.CompleteNewPasswordAsync("s2", It.IsAny<string>()))
            .ThrowsAsync(new CognitoException("InvalidPasswordException", "Password not long enough"))
            .ReturnsAsync(Tokens(idToken));

        // Act
        var first = await sut.CompleteNewPasswordAsync("short");
        var second = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal("Password not long enough", first.Error);
        Assert.True(second.Success);
    }

    [Theory]
    [InlineData("NotAuthorizedException", "Your sign-in has expired. Please sign in again.")]
    [InlineData("TooManyRequestsException", "Too many attempts. Please wait a moment and try again.")]
    public async Task GivenOtherCognitoError_WhenCompleteNewPasswordAsync_ThenFailsAndClearsChallenge(string errorType, string expected)
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito.Setup(x => x.CompleteNewPasswordAsync("s2", It.IsAny<string>())).ThrowsAsync(new CognitoException(errorType, "x"));

        // Act
        var result = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal(expected, result.Error);
        Assert.Equal("Please sign in again.", (await sut.CompleteNewPasswordAsync("NewPassword123")).Error);
    }

    [Fact]
    public async Task GivenNetworkFailure_WhenCompleteNewPasswordAsync_ThenKeepsChallenge()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito
            .SetupSequence(x => x.CompleteNewPasswordAsync("s2", It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(Tokens(idToken));

        // Act
        var first = await sut.CompleteNewPasswordAsync("NewPassword123");
        var second = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal("Could not reach the sign-in service. Check your connection and try again.", first.Error);
        Assert.True(second.Success);
    }

    [Fact]
    public async Task GivenNewLogin_WhenCompleteNewPasswordForEarlierChallenge_ThenAsksToSignInAgain()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "Incorrect username or password."));
        await sut.LoginAsync("someone@example.com", "wrong");

        // Act
        var result = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal("Please sign in again.", result.Error);
    }

    [Fact]
    public async Task GivenSessionWithRefreshToken_WhenLogoutAsync_ThenRemovesSessionAndRevokesToken()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, RefreshToken = "refresh" });
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().LogoutAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        mockCognito.Verify(x => x.RevokeAsync("refresh"), Times.Once);
    }

    [Fact]
    public async Task GivenRevokeFails_WhenLogoutAsync_ThenStillSignsOut()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, RefreshToken = "refresh" });
        mockCognito.Setup(x => x.RevokeAsync("refresh")).ThrowsAsync(new HttpRequestException("offline"));

        // Act
        await CreateSut().LogoutAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenNoSession_WhenLogoutAsync_ThenDoesNotCallCognito()
    {
        // Arrange
        SetupStoredSession(null);

        // Act
        await CreateSut().LogoutAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        mockCognito.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNoSession_WhenGetCurrentUserAsync_ThenReturnsNull()
    {
        // Arrange
        SetupStoredSession(null);

        // Act & Assert
        Assert.Null(await CreateSut().GetCurrentUserAsync());
    }

    [Fact]
    public async Task GivenSessionWithoutTokens_WhenGetCurrentUserAsync_ThenClearsIt()
    {
        // Arrange
        SetupStoredSession(new UserSession { Username = "bob", ExpiresAt = Now.AddHours(1) });

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenFreshSession_WhenGetCurrentUserAsync_ThenReturnsItWithoutRefreshing()
    {
        // Arrange
        var session = new UserSession { IdToken = idToken, RefreshToken = "refresh", ExpiresAt = Now.Add(CognitoAuthenticationService.RefreshMargin).AddSeconds(1) };
        SetupStoredSession(session);

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Same(session, result);
        mockCognito.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenSessionAboutToExpire_WhenGetCurrentUserAsync_ThenRefreshesAndKeepsRefreshToken()
    {
        // Arrange
        var signedInAt = Now.AddDays(-2);
        SetupStoredSession(new UserSession { Username = "bob.hoskins@example.com", IdToken = "old", RefreshToken = "refresh", SignedInAt = signedInAt, ExpiresAt = Now.AddMinutes(1) });
        var refreshed = Tokens(idToken);
        refreshed.RefreshToken = null;
        mockCognito.Setup(x => x.RefreshAsync("bob.hoskins@example.com", "refresh")).ReturnsAsync(refreshed);

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(idToken, result.IdToken);
        Assert.Equal("refresh", result.RefreshToken);
        Assert.Equal(signedInAt, result.SignedInAt);
        Assert.Equal(Now.AddHours(1), result.ExpiresAt);
        mockLocalStorage.Verify(x => x.SetObjectAsync(CognitoAuthenticationService.SessionStorageKey, result), Times.Once);
    }

    [Fact]
    public async Task GivenExpiredSessionWithoutRefreshToken_WhenGetCurrentUserAsync_ThenClearsIt()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(-1) });

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenRefreshRejected_WhenGetCurrentUserAsync_ThenClearsSession()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, RefreshToken = "refresh", ExpiresAt = Now.AddMinutes(-1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Refresh Token has expired"));

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenRefreshReturnsNoTokens_WhenGetCurrentUserAsync_ThenClearsSession()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, RefreshToken = "refresh", ExpiresAt = Now.AddMinutes(-1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ReturnsAsync(new CognitoSignInResult());

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenOffline_WhenGetCurrentUserAsyncNeedsRefresh_ThenReturnsNullButKeepsSession()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, RefreshToken = "refresh", ExpiresAt = Now.AddMinutes(-1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    private static string CreateJwt(string payloadJson) => new JsonWebTokenHandler().CreateToken(payloadJson);

    private static CognitoSignInResult Tokens(string idToken)
    {
        return new CognitoSignInResult { IdToken = idToken, AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 };
    }

    private async Task<CognitoAuthenticationService> CreateSutAwaitingNewPasswordAsync()
    {
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CognitoSignInResult { ChallengeName = "NEW_PASSWORD_REQUIRED", SessionId = "s2" });
        var sut = CreateSut();
        var result = await sut.LoginAsync("bob@example.com", Password);
        Assert.True(result.NewPasswordRequired);
        Assert.Null(result.Error);
        return sut;
    }

//#if (IncludeNotes)
    [Fact]
    public void GivenNullOfflineModeStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("offlineModeStore", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object, null!, mockSystemClock.Object)).ParamName);
    }

    [Fact]
    public async Task GivenExpiredSessionInOfflineMode_WhenGetCurrentUserAsync_ThenStoredSessionKeptWithoutRefreshing()
    {
        // Arrange
        var session = new UserSession { Username = "bob", IdToken = idToken, RefreshToken = "refresh", ExpiresAt = Now.AddMinutes(-30) };
        SetupStoredSession(session);
        mockOfflineMode.Setup(x => x.GetAsync()).ReturnsAsync(true);
        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert (the strict Cognito mock would throw if a refresh were attempted)
        Assert.Same(session, result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

//#endif
    private void SetupStoredSession(UserSession? session)
    {
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<UserSession>(CognitoAuthenticationService.SessionStorageKey))
            .ReturnsAsync(session);
    }

    private CognitoAuthenticationService CreateSut() => new(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
        mockOfflineMode.Object,
//#endif
        mockSystemClock.Object);
}
