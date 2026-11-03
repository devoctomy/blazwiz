using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class CognitoUserPoolClientTests
{
    private const string SessionId = "session-0123456789abcdef";

    private readonly Mock<IAmazonCognitoIdentityProvider> mockProvider = new();
    private readonly AwsCognitoConfig config = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client-123" };

    [Fact]
    public void GivenNullProvider_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new CognitoUserPoolClient(null!, config));

        // Assert
        Assert.Equal("provider", exception.ParamName);
    }

    [Fact]
    public void GivenNullConfig_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new CognitoUserPoolClient(mockProvider.Object, null!));

        // Assert
        Assert.Equal("cognitoConfig", exception.ParamName);
    }

    [Fact]
    public async Task GivenRefreshToken_WhenRefreshAsync_ThenReturnsNewTokens()
    {
        // Arrange
        InitiateAuthRequest? request = null;
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InitiateAuthRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", ExpiresIn = 3600 },

                // Always set by the SDK on real responses, and read by the library.
                ResponseMetadata = new ResponseMetadata(),
            });

        // Act
        var result = await CreateSut().RefreshAsync("bob@example.com", "refresh");

        // Assert
        Assert.Equal("id", result.IdToken);
        Assert.Equal("access", result.AccessToken);
        Assert.Equal("refresh", result.RefreshToken); // The library carries the original refresh token over.
        Assert.Equal(3600, result.ExpiresIn);
        Assert.NotNull(request);
        Assert.Equal(AuthFlowType.REFRESH_TOKEN_AUTH, request.AuthFlow);
        Assert.Equal("client-123", request.ClientId);
        Assert.Equal("refresh", request.AuthParameters["REFRESH_TOKEN"]);
    }

    [Fact]
    public async Task GivenRefreshToken_WhenRevokeAsync_ThenRevokesForClient()
    {
        // Arrange
        mockProvider.Setup(x => x.RevokeTokenAsync(It.IsAny<RevokeTokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RevokeTokenResponse());

        // Act
        await CreateSut().RevokeAsync("refresh");

        // Assert
        mockProvider.Verify(x => x.RevokeTokenAsync(It.Is<RevokeTokenRequest>(r => r.Token == "refresh" && r.ClientId == "client-123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenCognitoRejects_WhenRefreshAsync_ThenThrowsCognitoExceptionWithErrorCode()
    {
        // Arrange
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Refresh Token has expired") { ErrorCode = "NotAuthorizedException" });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => CreateSut().RefreshAsync("bob@example.com", "refresh"));

        // Assert
        Assert.Equal("NotAuthorizedException", exception.ErrorType);
        Assert.Equal("Refresh Token has expired", exception.Message);
    }

    [Fact]
    public async Task GivenNetworkFailureWrappedBySdk_WhenRevokeAsync_ThenThrowsHttpRequestException()
    {
        // Arrange
        mockProvider
            .Setup(x => x.RevokeTokenAsync(It.IsAny<RevokeTokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("Network failure", new HttpRequestException("offline")));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSut().RevokeAsync("refresh"));
    }

    [Fact]
    public async Task GivenNoChallenge_WhenCompleteNewPasswordAsync_ThenThrowsInvalidOperation()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().CompleteNewPasswordAsync("s1", "NewPassword123"));
    }

    [Fact]
    public async Task GivenWrongPassword_WhenSignInAsync_ThenThrowsCognitoException()
    {
        // Arrange - Cognito rejects the user before any challenge (e.g. too many attempts).
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Password attempts exceeded") { ErrorCode = "NotAuthorizedException" });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => CreateSut().SignInAsync("bob@example.com", "password"));

        // Assert
        Assert.Equal("NotAuthorizedException", exception.ErrorType);
        mockProvider.Verify(
            x => x.InitiateAuthAsync(It.Is<InitiateAuthRequest>(r => r.AuthFlow == AuthFlowType.USER_SRP_AUTH && r.AuthParameters["USERNAME"] == "bob@example.com"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenCorrectPassword_WhenSignInAsync_ThenReturnsTokens()
    {
        // Arrange
        RespondToAuthChallengeRequest? request = null;
        SetupPasswordVerifierChallenge();
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.IsAny<RespondToAuthChallengeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RespondToAuthChallengeRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 },
                ResponseMetadata = new ResponseMetadata(),
            });

        // Act
        var result = await CreateSut().SignInAsync("bob@example.com", "password");

        // Assert
        Assert.Equal("id", result.IdToken);
        Assert.Equal("access", result.AccessToken);
        Assert.Equal("refresh", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Null(result.ChallengeName);
        Assert.NotNull(request);
        Assert.Equal(ChallengeNameType.PASSWORD_VERIFIER, request.ChallengeName);
        Assert.Equal("client-123", request.ClientId);
    }

    [Fact]
    public async Task GivenCorrectPassword_WhenSignInAsync_ThenCannotCompleteNewPassword()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupRespondToAuthChallengeReturnsTokens();
        var sut = CreateSut();

        // Act
        await sut.SignInAsync("bob@example.com", "password");

        // Assert - no challenge is left waiting.
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteNewPasswordAsync("s1", "NewPassword123"));
    }

    [Fact]
    public async Task GivenNewPasswordRequired_WhenSignInAsync_ThenReturnsChallengeWithoutTokens()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupNewPasswordRequiredChallenge();

        // Act
        var result = await CreateSut().SignInAsync("bob@example.com", "password");

        // Assert
        Assert.Equal("NEW_PASSWORD_REQUIRED", result.ChallengeName);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Null(result.IdToken);
        Assert.Null(result.AccessToken);
        Assert.Null(result.RefreshToken);
        Assert.Equal(0, result.ExpiresIn);
    }

    [Fact]
    public async Task GivenNewPasswordRequired_WhenCompleteNewPasswordAsync_ThenReturnsTokens()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupNewPasswordRequiredChallenge();
        var sut = CreateSut();
        await sut.SignInAsync("bob@example.com", "password");
        RespondToAuthChallengeRequest? request = null;
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED), It.IsAny<CancellationToken>()))
            .Callback<RespondToAuthChallengeRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 },
                ResponseMetadata = new ResponseMetadata(),
            });

        // Act
        var result = await sut.CompleteNewPasswordAsync(SessionId, "NewPassword123");

        // Assert
        Assert.Equal("id", result.IdToken);
        Assert.NotNull(request);
        Assert.Equal(SessionId, request.Session);
        Assert.Equal("NewPassword123", request.ChallengeResponses["NEW_PASSWORD"]);
        Assert.Equal("bob@example.com", request.ChallengeResponses["USERNAME"]);
    }

    [Fact]
    public async Task GivenNewPasswordCompleted_WhenCompleteNewPasswordAsyncAgain_ThenThrowsInvalidOperation()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupNewPasswordRequiredChallenge();
        var sut = CreateSut();
        await sut.SignInAsync("bob@example.com", "password");
        SetupRespondToAuthChallengeReturnsTokens();
        await sut.CompleteNewPasswordAsync(SessionId, "NewPassword123");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteNewPasswordAsync(SessionId, "NewPassword123"));
    }

    [Fact]
    public async Task GivenNewPasswordRejected_WhenCompleteNewPasswordAsync_ThenChallengeCanBeRetried()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupNewPasswordRequiredChallenge();
        var sut = CreateSut();
        await sut.SignInAsync("bob@example.com", "password");
        mockProvider
            .SetupSequence(x => x.RespondToAuthChallengeAsync(It.IsAny<RespondToAuthChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPasswordException("Password does not conform to policy") { ErrorCode = "InvalidPasswordException" })
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 },
                ResponseMetadata = new ResponseMetadata(),
            });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => sut.CompleteNewPasswordAsync(SessionId, "weak"));
        var result = await sut.CompleteNewPasswordAsync(SessionId, "NewPassword123");

        // Assert
        Assert.Equal("InvalidPasswordException", exception.ErrorType);
        Assert.Equal("id", result.IdToken);
    }

    [Fact]
    public async Task GivenCognitoExceptionWithoutErrorCode_WhenRefreshAsync_ThenErrorTypeIsExceptionName()
    {
        // Arrange
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TooManyRequestsException("Rate exceeded"));

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => CreateSut().RefreshAsync("bob@example.com", "refresh"));

        // Assert
        Assert.Equal(nameof(TooManyRequestsException), exception.ErrorType);
        Assert.Equal("Rate exceeded", exception.Message);
    }

    private void SetupPasswordVerifierChallenge()
    {
        // Dummy SRP values: the library does the SRP calculation, and the mocked Cognito accepts whatever it sends.
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateAuthResponse
            {
                ChallengeName = ChallengeNameType.PASSWORD_VERIFIER,
                ChallengeParameters = new Dictionary<string, string>
                {
                    ["SALT"] = "abc123",
                    ["SECRET_BLOCK"] = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
                    ["SRP_B"] = "2",
                    ["USERNAME"] = "bob@example.com",
                    ["USER_ID_FOR_SRP"] = "bob@example.com",
                },
                ResponseMetadata = new ResponseMetadata(),
            });
    }

    [Fact]
    public async Task GivenSignedInUser_WhenChangePasswordAsync_ThenCognitoChangesItWithTheAccessToken()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResponse { ResponseMetadata = new ResponseMetadata() });
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("bob@example.com", "id", "access", DateTimeOffset.UtcNow.AddMinutes(30), "old-pass", "new-pass");

        // Assert
        mockProvider.Verify(
            x => x.ChangePasswordAsync(It.Is<ChangePasswordRequest>(r => r.AccessToken == "access" && r.PreviousPassword == "old-pass" && r.ProposedPassword == "new-pass"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenWrongCurrentPassword_WhenChangePasswordAsync_ThenThrowsCognitoException()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Incorrect username or password.") { ErrorCode = "NotAuthorizedException" });
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => sut.ChangePasswordAsync("bob@example.com", "id", "access", DateTimeOffset.UtcNow.AddMinutes(30), "wrong", "new-pass"));

        // Assert
        Assert.Equal("NotAuthorizedException", exception.ErrorType);
    }

    [Fact]
    public async Task GivenUsername_WhenForgotPasswordAsync_ThenCognitoSendsACode()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForgotPasswordResponse { ResponseMetadata = new ResponseMetadata() });
        var sut = CreateSut();

        // Act
        await sut.ForgotPasswordAsync("bob@example.com");

        // Assert
        mockProvider.Verify(
            x => x.ForgotPasswordAsync(It.Is<ForgotPasswordRequest>(r => r.Username == "bob@example.com" && r.ClientId == "client-123"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenCode_WhenConfirmForgotPasswordAsync_ThenCognitoSetsTheNewPassword()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ConfirmForgotPasswordAsync(It.IsAny<ConfirmForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmForgotPasswordResponse { ResponseMetadata = new ResponseMetadata() });
        var sut = CreateSut();

        // Act
        await sut.ConfirmForgotPasswordAsync("bob@example.com", "123456", "new-pass");

        // Assert
        mockProvider.Verify(
            x => x.ConfirmForgotPasswordAsync(It.Is<ConfirmForgotPasswordRequest>(r => r.Username == "bob@example.com" && r.ConfirmationCode == "123456" && r.Password == "new-pass" && r.ClientId == "client-123"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupNewPasswordRequiredChallenge()
    {
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.PASSWORD_VERIFIER), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
                Session = SessionId,
                ChallengeParameters = new Dictionary<string, string>(),
                ResponseMetadata = new ResponseMetadata(),
            });
    }

    private void SetupRespondToAuthChallengeReturnsTokens()
    {
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.IsAny<RespondToAuthChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 },
                ResponseMetadata = new ResponseMetadata(),
            });
    }

    private CognitoUserPoolClient CreateSut() => new(mockProvider.Object, config);
}
