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

// The code from the authenticator app, setting one up at sign in, and remembered devices.
public class CognitoAuthenticationServiceMfaTests
{
    private const string Username = "bob.hoskins@example.com";
    private const string Password = "CorrectHorse123";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 30, 0, TimeSpan.Zero);

    private readonly Mock<ICognitoUserPoolClient> mockCognito = new(MockBehavior.Strict);
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<TimeProvider> mockTimeProvider = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly CognitoCurrentDevice currentDevice = new();
    private readonly InMemoryOfflineSessionStore offlineSessions;
    private readonly string idToken = new JsonWebTokenHandler().CreateToken("""{"email":"bob.hoskins@example.com"}""");

    public CognitoAuthenticationServiceMfaTests()
    {
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now);
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername(It.IsAny<string>())).Returns("Bob Hoskins");
        offlineSessions = new InMemoryOfflineSessionStore(mockTimeProvider.Object);
    }

    public static TheoryData<Exception> Unreachable => new()
    {
        new HttpRequestException("offline"),
        new TaskCanceledException("timed out"),
    };

    public static TheoryData<Exception> Refused => new()
    {
        new CognitoException("InvalidParameterException", "bad verifier"),
    };

    public static TheoryData<string> UnreadableDevices => new()
    {
        "not json",
        "null",
        "{}",
        """{"DeviceKey":"dk","DeviceGroupKey":"dg"}""",
    };

    [Fact]
    public async Task GivenMfaChallenge_WhenLoginAsync_ThenAsksForTheCode()
    {
        // Arrange
        SetupSignIn(Challenge("SOFTWARE_TOKEN_MFA", "s2"));

        // Act
        var result = await CreateSut().LoginAsync(Username, Password);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.MfaCodeRequired);
        Assert.Null(result.MfaSetupSecret);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenCompleteMfaAsync_ThenSignedIn_WithTheCodeTrimmed()
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();
        mockCognito.Setup(x => x.RespondToMfaAsync("s2", "123456")).ReturnsAsync(Tokens());

        // Act
        var result = await sut.CompleteMfaAsync(" 123456 ");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(Username, result.Session?.Username);
        mockLocalStorage.Verify(x => x.SetObjectAsync(CognitoAuthenticationService.SessionStorageKey, It.IsAny<UserSession>()), Times.Once);
    }

    [Fact]
    public async Task GivenWrongCode_WhenCompleteMfaAsync_ThenCodeMismatch_AndTheCodeCanBeTriedAgain()
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();
        mockCognito.SetupSequence(x => x.RespondToMfaAsync("s2", It.IsAny<string>()))
            .ThrowsAsync(new CognitoException("CodeMismatchException", "Invalid code received for user"))
            .ReturnsAsync(Tokens());

        // Act
        var wrong = await sut.CompleteMfaAsync("000000");
        var right = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.False(wrong.Success);
        Assert.Equal(AuthErrorCodes.CodeMismatch, wrong.ErrorCode);
        Assert.True(right.Success);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GivenNoCode_WhenCompleteMfaAsync_ThenMfaCodeRequired_WithoutCallingCognito(string code)
    {
        // Arrange (RespondToMfaAsync is not set up: the strict mock fails if it is called)
        var sut = await CreateSutAwaitingCodeAsync();

        // Act
        var result = await sut.CompleteMfaAsync(code);

        // Assert
        Assert.Equal(AuthErrorCodes.MfaCodeRequired, result.ErrorCode);
    }

    [Fact]
    public async Task GivenNoSignInWaiting_WhenCompleteMfaAsync_ThenSignInAgain()
    {
        // Act
        var result = await CreateSut().CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenNewPasswordWaiting_WhenCompleteMfaAsync_ThenSignInAgain()
    {
        // Arrange
        SetupSignIn(Challenge("NEW_PASSWORD_REQUIRED", "s2"));
        var sut = CreateSut();
        await sut.LoginAsync(Username, Password);

        // Act
        var result = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenSessionExpired_WhenCompleteMfaAsync_ThenSignInExpired_AndNothingLeftWaiting()
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();
        mockCognito.Setup(x => x.RespondToMfaAsync("s2", "123456")).ThrowsAsync(new CognitoException("NotAuthorizedException", "Invalid session for the user, session is expired."));

        // Act
        var expired = await sut.CompleteMfaAsync("123456");
        var again = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInExpired, expired.ErrorCode);
        Assert.Equal(AuthErrorCodes.SignInAgain, again.ErrorCode);
    }

    [Fact]
    public async Task GivenCognitoThrottles_WhenCompleteMfaAsync_ThenTooManyAttempts()
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();
        mockCognito.Setup(x => x.RespondToMfaAsync("s2", "123456")).ThrowsAsync(new CognitoException("TooManyRequestsException", "Rate exceeded"));

        // Act
        var result = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.TooManyAttempts, result.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(Unreachable))]
    public async Task GivenCognitoUnreachable_WhenCompleteMfaAsync_ThenUnreachable_AndTheCodeCanBeTriedAgain(Exception failure)
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();
        mockCognito.SetupSequence(x => x.RespondToMfaAsync("s2", "123456"))
            .ThrowsAsync(failure)
            .ReturnsAsync(Tokens());

        // Act
        var unreachable = await sut.CompleteMfaAsync("123456");
        var retried = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.Unreachable, unreachable.ErrorCode);
        Assert.True(retried.Success);
    }

    [Fact]
    public async Task GivenNewPasswordThenMfa_WhenCompleteNewPasswordAsync_ThenAsksForTheCode()
    {
        // Arrange
        SetupSignIn(Challenge("NEW_PASSWORD_REQUIRED", "s2"));
        mockCognito.Setup(x => x.CompleteNewPasswordAsync("s2", "NewPassword123")).ReturnsAsync(Challenge("SOFTWARE_TOKEN_MFA", "s3"));
        mockCognito.Setup(x => x.RespondToMfaAsync("s3", "123456")).ReturnsAsync(Tokens());
        var sut = CreateSut();
        await sut.LoginAsync(Username, Password);

        // Act
        var newPassword = await sut.CompleteNewPasswordAsync("NewPassword123");
        var code = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.True(newPassword.MfaCodeRequired);
        Assert.True(code.Success);
    }

    [Fact]
    public async Task GivenMfaSetupChallenge_WhenLoginAsync_ThenTheSecretToSetUpTheAppWith()
    {
        // Arrange
        SetupSignIn(Challenge("MFA_SETUP", "s2"));
        mockCognito.Setup(x => x.BeginMfaSetupAsync("s2")).ReturnsAsync(("SECRETBASE32", "s3"));

        // Act
        var result = await CreateSut().LoginAsync(Username, Password);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.MfaCodeRequired);
        Assert.Equal("SECRETBASE32", result.MfaSetupSecret);
    }

    [Fact]
    public async Task GivenMfaSetupStarted_WhenCompleteMfaSetupAsync_ThenSignedIn_WithTheSetUpSession()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockCognito.Setup(x => x.CompleteMfaSetupAsync("s3", "123456")).ReturnsAsync(Tokens());

        // Act
        var result = await sut.CompleteMfaSetupAsync("123456 ");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GivenWrongFirstCode_WhenCompleteMfaSetupAsync_ThenCodeMismatch_AndCanBeTriedAgain()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockCognito.SetupSequence(x => x.CompleteMfaSetupAsync("s3", It.IsAny<string>()))
            .ThrowsAsync(new CognitoException("CodeMismatchException", "The code from the authenticator app was not accepted."))
            .ReturnsAsync(Tokens());

        // Act
        var wrong = await sut.CompleteMfaSetupAsync("000000");
        var right = await sut.CompleteMfaSetupAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.CodeMismatch, wrong.ErrorCode);
        Assert.True(right.Success);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenCompleteMfaSetupAsync_ThenSignInAgain()
    {
        // Arrange
        var sut = await CreateSutAwaitingCodeAsync();

        // Act
        var result = await sut.CompleteMfaSetupAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenMfaSetupCannotStart_WhenLoginAsync_ThenFails()
    {
        // Arrange
        SetupSignIn(Challenge("MFA_SETUP", "s2"));
        mockCognito.Setup(x => x.BeginMfaSetupAsync("s2")).ThrowsAsync(new CognitoException("InternalErrorException", "boom"));

        // Act
        var result = await CreateSut().LoginAsync(Username, Password);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(AuthErrorCodes.SignInFailed, result.ErrorCode);
    }

    [Fact]
    public async Task GivenRememberedDevice_WhenLoginAsync_ThenSignsInWithIt_AndItIsThisSessionsDevice()
    {
        // Arrange
        mockCognito
            .Setup(x => x.SignInAsync(Username, Password, It.Is<CognitoDeviceCredential>(d => d.DeviceKey == "dk" && d.DeviceGroupKey == "dg" && d.DevicePassword == "dp")))
            .ReturnsAsync(Tokens());

        // Act
        var result = await CreateSut().LoginAsync(Username, Password, Credential("dk", "dg", "dp"));

        // Assert
        Assert.True(result.Success);
        Assert.False(result.RememberedDeviceRejected);
        Assert.Equal("dk", currentDevice.DeviceKey);
    }

    [Fact]
    public async Task GivenForgottenDevice_WhenLoginAsync_ThenSignsInWithoutIt_AndSaysItWasRejected()
    {
        // Arrange
        mockCognito
            .Setup(x => x.SignInAsync(Username, Password, It.IsNotNull<CognitoDeviceCredential>()))
            .ThrowsAsync(new CognitoException("ResourceNotFoundException", "Device does not exist."));
        SetupSignIn(Challenge("SOFTWARE_TOKEN_MFA", "s2"));

        // Act
        var result = await CreateSut().LoginAsync(Username, Password, Credential("dk", "dg", "dp"));

        // Assert
        Assert.True(result.MfaCodeRequired);
        Assert.True(result.RememberedDeviceRejected);
        Assert.Null(currentDevice.DeviceKey);
    }

    [Theory]
    [MemberData(nameof(UnreadableDevices))]
    public async Task GivenUnreadableDevice_WhenLoginAsync_ThenSignsInWithoutIt_AndSaysItWasRejected(string rememberedDevice)
    {
        // Arrange
        SetupSignIn(Tokens());

        // Act
        var result = await CreateSut().LoginAsync(Username, Password, rememberedDevice);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.RememberedDeviceRejected);
    }

    [Fact]
    public async Task GivenWrongPasswordFromARememberedDevice_WhenLoginAsync_ThenNotAuthorized()
    {
        // Arrange
        mockCognito
            .Setup(x => x.SignInAsync(Username, "wrong", It.IsNotNull<CognitoDeviceCredential>()))
            .ThrowsAsync(new CognitoException("NotAuthorizedException", "Incorrect username or password."));

        // Act
        var result = await CreateSut().LoginAsync(Username, "wrong", Credential("dk", "dg", "dp"));

        // Assert
        Assert.Equal(AuthErrorCodes.NotAuthorized, result.ErrorCode);
        Assert.False(result.RememberedDeviceRejected);
    }

    [Fact]
    public async Task GivenSignInFromANewDevice_WhenRememberDeviceAsync_ThenConfirmed_AndItsCredentialReturned()
    {
        // Arrange
        var sut = await CreateSignedInFromNewDeviceAsync();
        mockCognito
            .Setup(x => x.ConfirmDeviceAsync("access", "nk", "ng", "Work laptop"))
            .ReturnsAsync(new CognitoDeviceCredential { DeviceKey = "nk", DeviceGroupKey = "ng", DevicePassword = "np" });

        // Act
        var credential = await sut.RememberDeviceAsync(" Work laptop ");
        var again = await sut.RememberDeviceAsync("Work laptop");

        // Assert
        var device = JsonSerializer.Deserialize<CognitoDeviceCredential>(credential!);
        Assert.Equal("nk", device?.DeviceKey);
        Assert.Equal("ng", device?.DeviceGroupKey);
        Assert.Equal("np", device?.DevicePassword);
        Assert.Equal("nk", currentDevice.DeviceKey);
        Assert.Null(again);
    }

    [Fact]
    public async Task GivenRememberedCredential_WhenLoginAsyncWithIt_ThenTheSameDeviceSignsIn()
    {
        // Arrange
        var sut = await CreateSignedInFromNewDeviceAsync();
        mockCognito
            .Setup(x => x.ConfirmDeviceAsync("access", "nk", "ng", "Work laptop"))
            .ReturnsAsync(new CognitoDeviceCredential { DeviceKey = "nk", DeviceGroupKey = "ng", DevicePassword = "np" });
        var credential = await sut.RememberDeviceAsync("Work laptop");
        mockCognito
            .Setup(x => x.SignInAsync(Username, Password, It.Is<CognitoDeviceCredential>(d => d.DeviceKey == "nk" && d.DevicePassword == "np")))
            .ReturnsAsync(Tokens());

        // Act
        var result = await sut.LoginAsync(Username, Password, credential);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.RememberedDeviceRejected);
    }

    [Fact]
    public async Task GivenSignInFromAKnownDevice_WhenRememberDeviceAsync_ThenNull()
    {
        // Arrange (ConfirmDeviceAsync is not set up: the strict mock fails if it is called)
        SetupSignIn(Tokens());
        var sut = CreateSut();
        await sut.LoginAsync(Username, Password);
        SetupStoredSession(ValidSession());

        // Act
        var result = await sut.RememberDeviceAsync("Work laptop");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenNullName_WhenRememberDeviceAsync_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().RememberDeviceAsync(null!));
    }

    [Fact]
    public async Task GivenChallengeWithoutAName_WhenLoginAsync_ThenUnsupportedWithAnEmptyName()
    {
        // Arrange
        SetupSignIn(new CognitoSignInResult { SessionId = "s2" });

        // Act
        var result = await CreateSut().LoginAsync(Username, Password);

        // Assert
        Assert.Equal(AuthErrorCodes.ChallengeUnsupported, result.ErrorCode);
        Assert.Equal([string.Empty], result.ErrorArguments);
    }

    [Fact]
    public async Task GivenSessionGone_WhenRememberDeviceAsync_ThenNull()
    {
        // Arrange
        var sut = await CreateSignedInFromNewDeviceAsync();
        SetupStoredSession(null);

        // Act
        var result = await sut.RememberDeviceAsync("Work laptop");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [MemberData(nameof(Unreachable))]
    [MemberData(nameof(Refused))]
    public async Task GivenConfirmingFails_WhenRememberDeviceAsync_ThenNull(Exception failure)
    {
        // Arrange
        var sut = await CreateSignedInFromNewDeviceAsync();
        mockCognito.Setup(x => x.ConfirmDeviceAsync("access", "nk", "ng", It.IsAny<string>())).ThrowsAsync(failure);

        // Act
        var result = await sut.RememberDeviceAsync("Work laptop");

        // Assert
        Assert.Null(result);
        Assert.Null(currentDevice.DeviceKey);
    }

    [Fact]
    public async Task GivenSignedInFromARememberedDevice_WhenLogoutAsync_ThenNoLongerThisSessionsDevice()
    {
        // Arrange
        mockCognito.Setup(x => x.SignInAsync(Username, Password, It.IsNotNull<CognitoDeviceCredential>())).ReturnsAsync(Tokens());
        mockCognito.Setup(x => x.RevokeAsync("refresh")).Returns(Task.CompletedTask);
        var sut = CreateSut();
        await sut.LoginAsync(Username, Password, Credential("dk", "dg", "dp"));

        // Act
        await sut.LogoutAsync();

        // Assert
        Assert.Null(currentDevice.DeviceKey);
    }

    [Fact]
    public async Task GivenSignInFromANewDevice_WhenSignInOfflineAsync_ThenNothingLeftToRemember()
    {
        // Arrange (ConfirmDeviceAsync is not set up: the strict mock fails if it is called)
        var sut = await CreateSignedInFromNewDeviceAsync();
        await sut.SignInOfflineAsync(Username, OfflineEntryReason.Chosen);

        // Act
        var result = await sut.RememberDeviceAsync("Work laptop");

        // Assert
        Assert.Null(result);
    }

    private static CognitoSignInResult Challenge(string name, string session) => new() { ChallengeName = name, SessionId = session };

    private static string Credential(string key, string group, string password) =>
        JsonSerializer.Serialize(new CognitoDeviceCredential { DeviceKey = key, DeviceGroupKey = group, DevicePassword = password });

    private CognitoSignInResult Tokens() => new() { IdToken = idToken, AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 };

    private UserSession ValidSession() => new() { Username = Username, IdToken = idToken, AccessToken = "access", ExpiresAt = Now.AddHours(1) };

    private void SetupSignIn(CognitoSignInResult result)
    {
        mockCognito.Setup(x => x.SignInAsync(Username, Password, null)).ReturnsAsync(result);
    }

    private void SetupStoredSession(UserSession? session)
    {
        mockLocalStorage.Setup(x => x.GetObjectAsync<UserSession>(CognitoAuthenticationService.SessionStorageKey)).ReturnsAsync(session);
    }

    private async Task<CognitoAuthenticationService> CreateSutAwaitingCodeAsync()
    {
        SetupSignIn(Challenge("SOFTWARE_TOKEN_MFA", "s2"));
        var sut = CreateSut();
        Assert.True((await sut.LoginAsync(Username, Password)).MfaCodeRequired);
        return sut;
    }

    private async Task<CognitoAuthenticationService> CreateSutAwaitingSetupAsync()
    {
        SetupSignIn(Challenge("MFA_SETUP", "s2"));
        mockCognito.Setup(x => x.BeginMfaSetupAsync("s2")).ReturnsAsync(("SECRETBASE32", "s3"));
        var sut = CreateSut();
        Assert.NotNull((await sut.LoginAsync(Username, Password)).MfaSetupSecret);
        return sut;
    }

    private async Task<CognitoAuthenticationService> CreateSignedInFromNewDeviceAsync()
    {
        var tokens = Tokens();
        tokens.NewDeviceKey = "nk";
        tokens.NewDeviceGroupKey = "ng";
        SetupSignIn(tokens);
        var sut = CreateSut();
        Assert.True((await sut.LoginAsync(Username, Password)).Success);
        SetupStoredSession(ValidSession());
        return sut;
    }

    private CognitoAuthenticationService CreateSut() => new(mockCognito.Object, mockLocalStorage.Object, mockUserDisplay.Object,
        offlineMode, offlineSessions, mockNotifier.Object, currentDevice, mockTimeProvider.Object);
}
