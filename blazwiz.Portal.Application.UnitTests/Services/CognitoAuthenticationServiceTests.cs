//#if (IncludeNotes)
using blazwiz.Core.Sync.Interfaces;
//#endif
using System.Text.Json;
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
    public async Task GivenSignedIn_WhenLogoutAsync_ThenRemovesSessionAndRevokesTokenOnce()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);

        // Act
        await sut.LogoutAsync();
        await sut.LogoutAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Exactly(2));
        mockCognito.Verify(x => x.RevokeAsync("refresh"), Times.Once);
    }

    [Fact]
    public async Task GivenRevokeFails_WhenLogoutAsync_ThenStillSignsOut()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        mockCognito.Setup(x => x.RevokeAsync("refresh")).ThrowsAsync(new HttpRequestException("offline"));

        // Act
        await sut.LogoutAsync();

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
        var session = new UserSession { IdToken = idToken, ExpiresAt = Now.Add(CognitoAuthenticationService.RefreshMargin).AddSeconds(1) };
        SetupStoredSession(session);

        // Act
        var result = await CreateSut().GetCurrentUserAsync();

        // Assert
        Assert.Same(session, result);
        mockCognito.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenSessionAboutToExpire_WhenGetCurrentUserAsync_ThenRefreshesWithTheTokenFromSignIn()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        var signedInAt = Now.AddHours(-2);
        SetupStoredSession(new UserSession { Username = "bob.hoskins@example.com", IdToken = "old", SignedInAt = signedInAt, ExpiresAt = Now.AddMinutes(1) });
        var refreshed = Tokens(idToken);
        refreshed.RefreshToken = null;
        mockCognito.Setup(x => x.RefreshAsync("bob.hoskins@example.com", "refresh")).ReturnsAsync(refreshed);

        // Act
        var first = await sut.GetCurrentUserAsync();
        var second = await sut.GetCurrentUserAsync();

        // Assert: the second refresh still has the token, though Cognito did not send it again.
        Assert.NotNull(first);
        Assert.Equal(idToken, first.IdToken);
        Assert.Equal(signedInAt, first.SignedInAt);
        Assert.Equal(Now.AddHours(1), first.ExpiresAt);
        Assert.NotNull(second);
        mockCognito.Verify(x => x.RefreshAsync("bob.hoskins@example.com", "refresh"), Times.Exactly(2));
        mockLocalStorage.Verify(x => x.SetObjectAsync(CognitoAuthenticationService.SessionStorageKey, first), Times.Once);
    }

    [Fact]
    public async Task GivenSignedInAndRefreshed_WhenSessionSaved_ThenRefreshTokenIsNeverStored()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { Username = "bob.hoskins@example.com", IdToken = "old", ExpiresAt = Now.AddMinutes(1) });
        mockCognito.Setup(x => x.RefreshAsync("bob.hoskins@example.com", "refresh")).ReturnsAsync(Tokens(idToken));

        // Act
        await sut.GetCurrentUserAsync();

        // Assert
        var saved = mockLocalStorage.Invocations.Where(i => i.Method.Name == nameof(ILocalStorageService.SetObjectAsync)).ToList();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, call => Assert.DoesNotContain("refresh", JsonSerializer.Serialize(call.Arguments[1])));
    }

    [Fact]
    public async Task GivenSessionAboutToExpireFromAnotherTabOrBeforeReload_WhenGetCurrentUserAsync_ThenEndsIt()
    {
        // Arrange
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(1) });
        var sut = CreateSut();
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenExpiredSession_WhenGetCurrentUserAsync_ThenNotRefreshedButRevokedAndEnded()
    {
        // Arrange (the strict Cognito mock would throw if a refresh were attempted)
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddSeconds(-1) });
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockCognito.Verify(x => x.RevokeAsync("refresh"), Times.Once);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenSessionExpiringNow_WhenGetCurrentUserAsync_ThenTreatedAsExpired()
    {
        // Arrange (the strict Cognito mock would throw if a refresh were attempted)
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now });
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);

        // Act & Assert
        Assert.Null(await sut.GetCurrentUserAsync());
        mockCognito.Verify(x => x.RevokeAsync("refresh"), Times.Once);
    }

    [Fact]
    public async Task GivenRefreshRejected_WhenGetCurrentUserAsync_ThenEndsSession()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Refresh Token has expired"));
        mockCognito.Setup(x => x.RevokeAsync("refresh")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Refresh Token has expired"));
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenRefreshReturnsNoTokens_WhenGetCurrentUserAsync_ThenEndsSession()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ReturnsAsync(new CognitoSignInResult());
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenNoNetworkWhileStillValid_WhenGetCurrentUserAsyncNeedsRefresh_ThenReturnsNullButKeepsSession()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(1) });
        mockCognito.Setup(x => x.RefreshAsync(It.IsAny<string>(), "refresh")).ThrowsAsync(new HttpRequestException("offline"));
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, ended);
    }

    [Fact]
    public async Task GivenSignedIn_WhenLogoutAsync_ThenSessionEndedNotRaised()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        await sut.LogoutAsync();

        // Assert
        Assert.Equal(0, ended);
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
        var session = new UserSession { Username = "bob", IdToken = idToken, ExpiresAt = Now.AddMinutes(-30) };
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

    // Signed in, so the service holds the refresh token "refresh" in memory.
    private async Task<CognitoAuthenticationService> CreateSignedInSutAsync()
    {
        mockCognito.Setup(x => x.SignInAsync("bob.hoskins@example.com", Password)).ReturnsAsync(Tokens(idToken));
        var sut = CreateSut();
        Assert.True((await sut.LoginAsync("bob.hoskins@example.com", Password)).Success);
        return sut;
    }

    private CognitoAuthenticationService CreateSut() => new(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object,
//#if (IncludeNotes)
        mockOfflineMode.Object,
//#endif
        mockSystemClock.Object);
}
