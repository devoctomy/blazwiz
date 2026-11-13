using System.Text.Json;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class CognitoAuthenticationServiceTests
{
    private const string Password = "CorrectHorse123";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 13, 10, 59, TimeSpan.Zero);

    private readonly Mock<ICognitoUserPoolClient> mockCognito = new(MockBehavior.Strict);
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<TimeProvider> mockTimeProvider = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly InMemoryOfflineSessionStore offlineSessions;
    private readonly string idToken = CreateJwt("""{"email":"bob.hoskins@example.com","cognito:username":"5f2c1a7e"}""");

    private DateTimeOffset now = Now;

    public CognitoAuthenticationServiceTests()
    {
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => now);
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername(It.IsAny<string>())).Returns("Bob Hoskins");
        offlineSessions = new InMemoryOfflineSessionStore(mockTimeProvider.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cognitoClient", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(null!, mockLocalStorage.Object, mockUserDisplay.Object,
            offlineMode, offlineSessions, mockNotifier.Object,
            mockTimeProvider.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, null!, mockUserDisplay.Object,
            offlineMode, offlineSessions, mockNotifier.Object,
            mockTimeProvider.Object)).ParamName);
        Assert.Equal("userDisplayService", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, null!,
            offlineMode, offlineSessions, mockNotifier.Object,
            mockTimeProvider.Object)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object,
            offlineMode, offlineSessions, mockNotifier.Object,
            null!)).ParamName);
    }

    [Fact]
    public void GivenService_WhenGettingIsPlaceholder_ThenFalse()
    {
        // Act & Assert
        Assert.False(CreateSut().IsPlaceholder);
    }

    [Fact]
    public void GivenService_WhenGettingRefreshMargin_ThenTheTokenRefreshMargin()
    {
        // Act & Assert
        Assert.Equal(CognitoAuthenticationService.TokenRefreshMargin, CreateSut().RefreshMargin);
    }

    [Theory]
    [InlineData("eu-west-2", "pool", "client", true)]
    [InlineData("", "pool", "client", false)]
    [InlineData("eu-west-2", " ", "client", false)]
    [InlineData("eu-west-2", "pool", "", false)]
    public void GivenConfig_WhenIsConfigured_ThenRequiresRegionPoolAndClient(string region, string pool, string client, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, CognitoAuthenticationService.IsConfigured(new AwsCognitoConfig { Region = region, UserPoolId = pool, UserPoolClientId = client }));
    }

    [Fact]
    public void GivenNullConfig_WhenIsConfigured_ThenFalse()
    {
        // Act & Assert
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
        Assert.Equal(AuthErrorCodes.CredentialsRequired, result.ErrorCode);
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
        Assert.True(result.Success, result.ErrorCode);
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
    [InlineData("NotAuthorizedException", "Incorrect username or password.", AuthErrorCodes.NotAuthorized)]
    [InlineData("NotAuthorizedException", "Password attempts exceeded", AuthErrorCodes.NotAuthorized)]
    [InlineData("UserNotFoundException", "x", AuthErrorCodes.NotAuthorized)]
    [InlineData("PasswordResetRequiredException", "x", AuthErrorCodes.PasswordResetRequired)]
    [InlineData("UserNotConfirmedException", "x", AuthErrorCodes.UserNotConfirmed)]
    [InlineData("TooManyRequestsException", "x", AuthErrorCodes.TooManyAttempts)]
    [InlineData("LimitExceededException", "x", AuthErrorCodes.TooManyAttempts)]
    [InlineData("InternalErrorException", "x", AuthErrorCodes.SignInFailed)]
    public async Task GivenCognitoError_WhenLoginAsync_ThenReturnsFriendlyError(string errorType, string message, string expected)
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new CognitoException(errorType, message));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorCode);
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
        Assert.Equal(AuthErrorCodes.Unreachable, result.ErrorCode);
        Assert.True(result.ServiceUnreachable);
    }

    [Fact]
    public async Task GivenTimeout_WhenLoginAsync_ThenUnreachable()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new TaskCanceledException("timed out"));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.True(result.ServiceUnreachable);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GivenSignInRefused_WhenLoginAsync_ThenNotUnreachable()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "User is disabled."));

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.ServiceUnreachable);
        Assert.False(result.Success);
    }

    [Fact]
    public void GivenNullOfflineDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object, null!, offlineSessions, mockNotifier.Object, mockTimeProvider.Object)).ParamName);
        Assert.Equal("offlineSessionStore", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, null!, mockNotifier.Object, mockTimeProvider.Object)).ParamName);
    }

    [Fact]
    public async Task GivenRemembered_WhenSignInOfflineAsync_ThenOfflineSessionWithoutCallingCognito()
    {
        // Arrange (the strict Cognito mock would throw on any call)
        using var sut = CreateSut();

        // Act
        var result = await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.Chosen);

        // Assert
        Assert.True(result.Success);
        var session = await sut.GetCurrentUserAsync();
        Assert.True(session!.IsOffline);
        Assert.Null(session.IdToken);
        Assert.Equal(Now.Add(OfflineModeService.SessionLength), session.ExpiresAt);
        Assert.Equal(OfflineEntryReason.Chosen, offlineMode.Reason);
        mockLocalStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineSessionExpired_WhenGetCurrentUserAsync_ThenEndedWithoutCallingCognito()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.Chosen);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;
        now = Now.Add(OfflineModeService.SessionLength);

        // Act & Assert
        Assert.Null(await sut.GetCurrentUserAsync());
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenOfflineModeTurnedOff_ThenSessionEnded()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.SignInUnreachable);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        offlineMode.Exit();

        // Assert
        Assert.Equal(1, ended);
        Assert.Null(offlineSessions.Session);
    }

    [Fact]
    public async Task GivenSignedInOnline_WhenGoOfflineAsync_ThenRefreshTokenRevokedFirstAndSessionOfflineForFifteenMinutes()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(new UserSession { Username = "bob.hoskins@example.com", DisplayName = "Bob Hoskins", IdToken = idToken, ExpiresAt = Now.AddHours(1) });
        var order = new List<string>();
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Callback(() => order.Add(offlineMode.IsOffline ? "revoke-offline" : "revoke-online")).Returns(Task.CompletedTask);

        // Act
        await sut.GoOfflineAsync();

        // Assert
        Assert.Equal(["revoke-online"], order);
        var session = await sut.GetCurrentUserAsync();
        Assert.True(session!.IsOffline);
        Assert.Equal(Now.Add(OfflineModeService.SessionLength), session.ExpiresAt);
        Assert.Null(session.IdToken);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenGoOfflineAsync_ThenThrows()
    {
        // Arrange
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GoOfflineAsync());
        Assert.False(offlineMode.IsOffline);
    }

    [Fact]
    public async Task GivenOfflineModeWithoutOfflineSession_WhenGetCurrentUserAsync_ThenNobodyAndStoredSessionNotUsed()
    {
        // Arrange (an expired stored session would otherwise be refreshed, a call to Cognito)
        SetupStoredSession(new UserSession { IdToken = idToken, ExpiresAt = Now.AddMinutes(1) });
        offlineMode.Enter(OfflineEntryReason.Chosen);
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetCurrentUserAsync());
        mockLocalStorage.Verify(x => x.GetObjectAsync<UserSession>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenLogoutAsync_ThenForgottenWithoutCallingCognito()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.Chosen);

        // Act
        await sut.LogoutAsync();

        // Assert
        Assert.Null(offlineSessions.Session);
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
        Assert.Equal(AuthErrorCodes.ChallengeUnsupported, result.ErrorCode);
        Assert.Equal(["SOFTWARE_TOKEN_MFA"], result.ErrorArguments);
    }

    [Fact]
    public async Task GivenNeitherTokensNorAChallenge_WhenLoginAsync_ThenFailsWithAnEmptyChallengeName()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CognitoSignInResult());

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.NewPasswordRequired);
        Assert.Equal(AuthErrorCodes.ChallengeUnsupported, result.ErrorCode);
        Assert.Equal([string.Empty], result.ErrorArguments);
        mockLocalStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenANewPasswordChallengeWithoutASession_WhenLoginAsync_ThenFailsAsUnsupported()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CognitoSignInResult { ChallengeName = "NEW_PASSWORD_REQUIRED" });

        // Act
        var result = await CreateSut().LoginAsync("bob@example.com", Password);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.NewPasswordRequired);
        Assert.Equal(AuthErrorCodes.ChallengeUnsupported, result.ErrorCode);
    }

    [Fact]
    public async Task GivenAlreadyOffline_WhenGoOfflineAsync_ThenNothingChangesAndCognitoNotCalled()
    {
        // Arrange (the strict Cognito mock would throw on any call)
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.SignInUnreachable);
        var before = offlineSessions.Session;

        // Act
        await sut.GoOfflineAsync();

        // Assert
        Assert.Same(before, offlineSessions.Session);
        Assert.Equal(OfflineEntryReason.SignInUnreachable, offlineMode.Reason);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenAPlainUsername_WhenSignInOfflineAsync_ThenTheOfflineSessionHasNoEmail()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.SignInOfflineAsync(" bob ", OfflineEntryReason.Chosen);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("bob", result.Session!.Username);
        Assert.Null(result.Session.Email);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenOfflineModeTurnedOffAndStorageFails_ThenTheErrorIsSwallowed()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SignInOfflineAsync("bob.hoskins@example.com", OfflineEntryReason.Chosen);
        mockLocalStorage.Setup(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey)).ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var exception = Record.Exception(() => offlineMode.Exit());

        // Assert
        Assert.Null(exception);
        Assert.Null(offlineSessions.Session);
        Assert.Equal(0, ended);
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
        Assert.Equal(AuthErrorCodes.SignInAgain, (await sut.CompleteNewPasswordAsync("NewPassword123")).ErrorCode);
    }

    [Fact]
    public async Task GivenNoPendingChallenge_WhenCompleteNewPasswordAsync_ThenAsksToSignInAgain()
    {
        // Act
        var result = await CreateSut().CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenPendingChallenge_WhenCompleteNewPasswordWithBlank_ThenAsksForPasswordAndKeepsChallenge()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();

        // Act
        var result = await sut.CompleteNewPasswordAsync(" ");

        // Assert
        Assert.Equal(AuthErrorCodes.NewPasswordRequired, result.ErrorCode);
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
        Assert.Equal(AuthErrorCodes.InvalidPassword, first.ErrorCode);
        Assert.True(second.Success);
    }

    [Theory]
    [InlineData("NotAuthorizedException", AuthErrorCodes.SignInExpired)]
    [InlineData("TooManyRequestsException", AuthErrorCodes.TooManyAttempts)]
    public async Task GivenOtherCognitoError_WhenCompleteNewPasswordAsync_ThenFailsAndClearsChallenge(string errorType, string expected)
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockCognito.Setup(x => x.CompleteNewPasswordAsync("s2", It.IsAny<string>())).ThrowsAsync(new CognitoException(errorType, "x"));

        // Act
        var result = await sut.CompleteNewPasswordAsync("NewPassword123");

        // Assert
        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(AuthErrorCodes.SignInAgain, (await sut.CompleteNewPasswordAsync("NewPassword123")).ErrorCode);
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
        Assert.Equal(AuthErrorCodes.Unreachable, first.ErrorCode);
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
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
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
        var session = new UserSession { IdToken = idToken, ExpiresAt = Now.Add(CognitoAuthenticationService.TokenRefreshMargin).AddSeconds(1) };
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

    public static TheoryData<Exception> CognitoFailures => new()
    {
        new HttpRequestException("offline"),
        new TaskCanceledException("timed out"),
        new CognitoException("TooManyRequestsException", "slow down"),
    };

    [Fact]
    public void GivenNullNotifier_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("signOutEverywhereNotifier", Assert.Throws<ArgumentNullException>(() => new CognitoAuthenticationService(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, offlineSessions, null!, mockTimeProvider.Object)).ParamName);
    }

    [Fact]
    public async Task GivenSignedIn_WhenSignOutEverywhereAsync_ThenGlobalSignOutThenHandlersThenSignedOut()
    {
        // Arrange (RevokeAsync is not set up: the strict mock fails if the revoked refresh token is revoked again)
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(ValidSession());
        var calls = new List<string>();
        mockCognito.Setup(x => x.GlobalSignOutAsync("access")).Callback(() => calls.Add("global")).Returns(Task.CompletedTask);
        mockNotifier.Setup(x => x.NotifyAsync(It.IsAny<UserSession>(), SignOutReason.UserRequested)).Callback(() => calls.Add("notify")).Returns(Task.CompletedTask);
        mockLocalStorage.Setup(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey)).Callback(() => calls.Add("removed")).Returns(Task.CompletedTask);
        var ended = 0;
        sut.SessionEnded += (_, _) => ended++;

        // Act
        var result = await sut.SignOutEverywhereAsync(SignOutReason.UserRequested);

        // Assert
        Assert.True(result);
        Assert.Equal(["global", "notify", "removed"], calls);
        Assert.Equal(0, ended);
    }

    [Fact]
    public async Task GivenTokenAlreadyRevoked_WhenSignOutEverywhereAsync_ThenStillSignedOutEverywhere()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(ValidSession());
        mockCognito.Setup(x => x.GlobalSignOutAsync("access")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Access Token has been revoked"));

        // Act
        var result = await sut.SignOutEverywhereAsync(SignOutReason.KeysRotated);

        // Assert
        Assert.True(result);
        mockNotifier.Verify(x => x.NotifyAsync(It.IsAny<UserSession>(), SignOutReason.KeysRotated), Times.Once);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
    }

    [Theory]
    [MemberData(nameof(CognitoFailures))]
    public async Task GivenGlobalSignOutFails_WhenSignOutEverywhereAsync_ThenFalse_AndNothingChanges(Exception failure)
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        SetupStoredSession(ValidSession());
        mockCognito.Setup(x => x.GlobalSignOutAsync("access")).ThrowsAsync(failure);

        // Act
        var result = await sut.SignOutEverywhereAsync(SignOutReason.UserRequested);

        // Assert
        Assert.False(result);
        mockNotifier.Verify(x => x.NotifyAsync(It.IsAny<UserSession>(), It.IsAny<SignOutReason>()), Times.Never);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenSignOutEverywhereAsync_ThenFalse_AndCognitoNotCalled()
    {
        // Arrange (the strict Cognito mock fails on any call)
        var sut = CreateSut();
        await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.Chosen);

        // Act
        var result = await sut.SignOutEverywhereAsync(SignOutReason.UserRequested);

        // Assert
        Assert.False(result);
        mockNotifier.Verify(x => x.NotifyAsync(It.IsAny<UserSession>(), It.IsAny<SignOutReason>()), Times.Never);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenSignOutEverywhereAsync_ThenFalse()
    {
        // Arrange
        SetupStoredSession(null);
        var sut = CreateSut();

        // Act
        var result = await sut.SignOutEverywhereAsync(SignOutReason.UserRequested);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GivenSignedIn_WhenEndSessionAsync_ThenSignedOut_AndSessionEndedRaisedWithReasonAndDevice()
    {
        // Arrange
        var sut = await CreateSignedInSutAsync();
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);
        SessionEndedEventArgs? args = null;
        sut.SessionEnded += (_, e) => args = e;

        // Act
        await sut.EndSessionAsync(SessionEndedReason.KeysRotated, "Android-Edge-624834");

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(SessionEndedReason.KeysRotated, args?.Reason);
        Assert.Equal("Android-Edge-624834", args?.DeviceName);
    }

    [Fact]
    public async Task GivenSessionAccepted_WhenCheckSessionAsync_ThenTrue_AndKept()
    {
        // Arrange
        SetupStoredSession(ValidSession());
        mockCognito.Setup(x => x.CheckAccessTokenAsync("access")).Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Act
        var result = await sut.CheckSessionAsync();

        // Assert
        Assert.True(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSessionRevoked_WhenCheckSessionAsync_ThenFalse_AndSessionEndedSaysRevoked()
    {
        // Arrange
        SetupStoredSession(ValidSession());
        mockCognito.Setup(x => x.CheckAccessTokenAsync("access")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Access Token has been revoked"));
        var sut = CreateSut();
        SessionEndedEventArgs? args = null;
        sut.SessionEnded += (_, e) => args = e;

        // Act
        var result = await sut.CheckSessionAsync();

        // Assert
        Assert.False(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(CognitoAuthenticationService.SessionStorageKey), Times.Once);
        Assert.Equal(SessionEndedReason.Revoked, args?.Reason);
        Assert.Null(args?.DeviceName);
    }

    [Theory]
    [MemberData(nameof(CognitoFailures))]
    public async Task GivenCheckFails_WhenCheckSessionAsync_ThenTrue_AndKept(Exception failure)
    {
        // Arrange
        SetupStoredSession(ValidSession());
        mockCognito.Setup(x => x.CheckAccessTokenAsync("access")).ThrowsAsync(failure);
        var sut = CreateSut();

        // Act
        var result = await sut.CheckSessionAsync();

        // Assert
        Assert.True(result);
        mockLocalStorage.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenCheckSessionAsync_ThenFalse()
    {
        // Arrange
        SetupStoredSession(null);
        var sut = CreateSut();

        // Act
        var result = await sut.CheckSessionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenCheckSessionAsync_ThenTrue_AndCognitoNotCalled()
    {
        // Arrange (the strict Cognito mock fails on any call)
        var sut = CreateSut();
        await sut.SignInOfflineAsync("bob@example.com", OfflineEntryReason.Chosen);

        // Act
        var result = await sut.CheckSessionAsync();

        // Assert
        Assert.True(result);
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
        Assert.Null(result.ErrorCode);
        return sut;
    }

    private UserSession ValidSession() => new() { Username = "bob.hoskins@example.com", IdToken = idToken, AccessToken = "access", ExpiresAt = Now.AddHours(1) };

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
        offlineMode, offlineSessions, mockNotifier.Object,
        mockTimeProvider.Object);
}
