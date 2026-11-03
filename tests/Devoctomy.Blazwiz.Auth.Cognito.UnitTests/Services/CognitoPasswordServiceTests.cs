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

public class CognitoPasswordServiceTests
{
    private static readonly DateTimeOffset ExpiresAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICognitoUserPoolClient> mockClient = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly OfflineModeService offlineMode = new();

    public CognitoPasswordServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession
        {
            Username = "bob@example.com",
            IdToken = "id",
            AccessToken = "access",
            ExpiresAt = ExpiresAt,
        });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("userPoolClient", Assert.Throws<ArgumentNullException>(() => new CognitoPasswordService(null!, mockAuth.Object, offlineMode)).ParamName);
        Assert.Equal("authenticationService", Assert.Throws<ArgumentNullException>(() => new CognitoPasswordService(mockClient.Object, null!, offlineMode)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new CognitoPasswordService(mockClient.Object, mockAuth.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedIn_WhenChangePasswordAsync_ThenChangedWithTheSessionsTokens()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("old", "new");

        // Assert
        mockClient.Verify(x => x.ChangePasswordAsync("bob@example.com", "id", "access", ExpiresAt, "old", "new"), Times.Once);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenChangePasswordAsync_ThenNotSignedIn()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", IsOffline = true });
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync("old", "new"));

        // Assert
        Assert.Equal(AuthErrorCodes.NotSignedIn, exception.Code);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenChangingOrResetting_ThenNoCallAndSaysWhy()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var sut = CreateSut();

        // Act
        var change = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync("old", "new"));
        var request = await Assert.ThrowsAsync<BlazwizException>(() => sut.RequestPasswordResetAsync("bob@example.com"));
        var reset = await Assert.ThrowsAsync<BlazwizException>(() => sut.ResetPasswordAsync("bob@example.com", "123456", "new"));

        // Assert
        Assert.All([change, request, reset], e => Assert.Equal(AuthErrorCodes.PasswordChangeOffline, e.Code));
        mockClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("NotAuthorizedException", AuthErrorCodes.CurrentPasswordWrong)]
    [InlineData("InvalidPasswordException", AuthErrorCodes.InvalidPassword)]
    [InlineData("LimitExceededException", AuthErrorCodes.TooManyAttempts)]
    [InlineData("InternalErrorException", AuthErrorCodes.PasswordChangeFailed)]
    public async Task GivenCognitoRefuses_WhenChangePasswordAsync_ThenSaysWhy(string errorType, string expected)
    {
        // Arrange
        mockClient
            .Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new CognitoException(errorType, "refused"));
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync("old", "new"));

        // Assert
        Assert.Equal(expected, exception.Code);
    }

    [Fact]
    public async Task GivenNoConnection_WhenChangePasswordAsync_ThenUnreachable()
    {
        // Arrange
        mockClient
            .Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync("old", "new"));

        // Assert
        Assert.Equal(AuthErrorCodes.Unreachable, exception.Code);
    }

    [Fact]
    public async Task GivenEmail_WhenRequestPasswordResetAsync_ThenCodeSentToTheTrimmedAddress()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RequestPasswordResetAsync(" bob@example.com ");

        // Assert
        mockClient.Verify(x => x.ForgotPasswordAsync("bob@example.com"), Times.Once);
    }

    [Fact]
    public async Task GivenUnknownEmail_WhenRequestPasswordResetAsync_ThenSucceedsSoAccountsAreNotRevealed()
    {
        // Arrange
        mockClient.Setup(x => x.ForgotPasswordAsync(It.IsAny<string>())).ThrowsAsync(new CognitoException("UserNotFoundException", "no such user"));
        var sut = CreateSut();

        // Act & Assert
        await sut.RequestPasswordResetAsync("nobody@example.com");
    }

    [Theory]
    [InlineData("CodeMismatchException", AuthErrorCodes.CodeMismatch)]
    [InlineData("ExpiredCodeException", AuthErrorCodes.CodeExpired)]
    [InlineData("UserNotFoundException", AuthErrorCodes.CodeMismatch)]
    [InlineData("InvalidPasswordException", AuthErrorCodes.InvalidPassword)]
    [InlineData("TooManyFailedAttemptsException", AuthErrorCodes.TooManyAttempts)]
    [InlineData("InternalErrorException", AuthErrorCodes.PasswordResetFailed)]
    public async Task GivenCognitoRefuses_WhenResetPasswordAsync_ThenSaysWhy(string errorType, string expected)
    {
        // Arrange
        mockClient
            .Setup(x => x.ConfirmForgotPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new CognitoException(errorType, "refused"));
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ResetPasswordAsync("bob@example.com", "123456", "new"));

        // Assert
        Assert.Equal(expected, exception.Code);
    }

    [Fact]
    public async Task GivenCode_WhenResetPasswordAsync_ThenConfirmedWithCognito()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ResetPasswordAsync(" bob@example.com ", " 123456 ", "new");

        // Assert
        mockClient.Verify(x => x.ConfirmForgotPasswordAsync("bob@example.com", "123456", "new"), Times.Once);
    }

    [Theory]
    [InlineData("", "123", "new", AuthErrorCodes.EmailRequired)]
    [InlineData("bob@example.com", "", "new", AuthErrorCodes.CodeRequired)]
    [InlineData("bob@example.com", "123", "", AuthErrorCodes.PasswordsRequired)]
    public async Task GivenMissingField_WhenResetPasswordAsync_ThenSaysWhichWithoutACall(string email, string code, string password, string expected)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ResetPasswordAsync(email, code, password));

        // Assert
        Assert.Equal(expected, exception.Code);
        mockClient.VerifyNoOtherCalls();
    }

    private CognitoPasswordService CreateSut() => new(mockClient.Object, mockAuth.Object, offlineMode);
}
