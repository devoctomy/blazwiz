using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class CognitoMfaServiceTests
{
    private readonly Mock<ICognitoUserPoolClient> mockClient = new(MockBehavior.Strict);
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly OfflineModeService offlineMode = new();

    public CognitoMfaServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", AccessToken = "access" });
    }

    public static TheoryData<Exception, string> Failures => new()
    {
        { new CognitoException("CodeMismatchException", "wrong"), AuthErrorCodes.CodeMismatch },
        { new CognitoException("EnableSoftwareTokenMFAException", "wrong"), AuthErrorCodes.CodeMismatch },
        { new CognitoException("TooManyRequestsException", "slow down"), AuthErrorCodes.TooManyAttempts },
        { new CognitoException("LimitExceededException", "slow down"), AuthErrorCodes.TooManyAttempts },
        { new CognitoException("NotAuthorizedException", "revoked"), AuthErrorCodes.SignInAgain },
        { new CognitoException("InternalErrorException", "boom"), AuthErrorCodes.MfaChangeFailed },
        { new HttpRequestException("offline"), AuthErrorCodes.Unreachable },
        { new TaskCanceledException("timed out"), AuthErrorCodes.Unreachable },
    };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("userPoolClient", Assert.Throws<ArgumentNullException>(() => new CognitoMfaService(null!, mockAuth.Object, offlineMode)).ParamName);
        Assert.Equal("authenticationService", Assert.Throws<ArgumentNullException>(() => new CognitoMfaService(mockClient.Object, null!, offlineMode)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new CognitoMfaService(mockClient.Object, mockAuth.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenSignedIn_WhenIsEnabledAsync_ThenWhatCognitoSays(bool enabled)
    {
        // Arrange
        mockClient.Setup(x => x.IsSoftwareTokenMfaEnabledAsync("access")).ReturnsAsync(enabled);

        // Act
        var result = await CreateSut().IsEnabledAsync();

        // Assert
        Assert.Equal(enabled, result);
    }

    [Fact]
    public async Task GivenSignedIn_WhenBeginSetupAsync_ThenANewSecret()
    {
        // Arrange
        mockClient.Setup(x => x.AssociateSoftwareTokenAsync("access")).ReturnsAsync("SECRETBASE32");

        // Act
        var secret = await CreateSut().BeginSetupAsync();

        // Assert
        Assert.Equal("SECRETBASE32", secret);
    }

    [Fact]
    public async Task GivenCode_WhenCompleteSetupAsync_ThenVerifiedThenTurnedOn()
    {
        // Arrange
        var calls = new List<string>();
        mockClient.Setup(x => x.VerifySoftwareTokenAsync("access", "123456")).Callback(() => calls.Add("verify")).Returns(Task.CompletedTask);
        mockClient.Setup(x => x.SetSoftwareTokenMfaAsync("access", true)).Callback(() => calls.Add("on")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().CompleteSetupAsync(" 123456 ");

        // Assert
        Assert.Equal(["verify", "on"], calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GivenNoCode_WhenCompleteSetupAsync_ThenMfaCodeRequired(string code)
    {
        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().CompleteSetupAsync(code));

        // Assert
        Assert.Equal(AuthErrorCodes.MfaCodeRequired, exception.Code);
    }

    [Fact]
    public async Task GivenSignedIn_WhenDisableAsync_ThenTurnedOff()
    {
        // Arrange
        mockClient.Setup(x => x.SetSoftwareTokenMfaAsync("access", false)).Returns(Task.CompletedTask);

        // Act
        await CreateSut().DisableAsync();

        // Assert
        mockClient.Verify(x => x.SetSoftwareTokenMfaAsync("access", false), Times.Once);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task GivenCognitoFails_WhenCompleteSetupAsync_ThenTheMatchingError(Exception failure, string expected)
    {
        // Arrange
        mockClient.Setup(x => x.VerifySoftwareTokenAsync("access", "123456")).ThrowsAsync(failure);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().CompleteSetupAsync("123456"));

        // Assert
        Assert.Equal(expected, exception.Code);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenAnyCall_ThenAccountChangeOffline_WithoutCallingCognito()
    {
        // Arrange (the strict client mock fails on any call)
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(sut.IsEnabledAsync)).Code);
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(sut.BeginSetupAsync)).Code);
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(() => sut.CompleteSetupAsync("123456"))).Code);
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(sut.DisableAsync)).Code);
    }

    public static TheoryData<UserSession?> NoOnlineSession => new()
    {
        null,
        new UserSession { Username = "bob@example.com", IsOffline = true },
        new UserSession { Username = "bob@example.com" },
    };

    [Theory]
    [MemberData(nameof(NoOnlineSession))]
    public async Task GivenNoOnlineSession_WhenDisableAsync_ThenNotSignedIn(UserSession? session)
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().DisableAsync());

        // Assert
        Assert.Equal(AuthErrorCodes.NotSignedIn, exception.Code);
    }

    private CognitoMfaService CreateSut() => new(mockClient.Object, mockAuth.Object, offlineMode);
}
