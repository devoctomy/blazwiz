using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Aws;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class CognitoUserPoolClientTests
{
    private readonly Mock<IAmazonCognitoIdentityProvider> mockProvider = new();
    private readonly AwsCognitoConfig config = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client-123" };

    [Fact]
    public void GivenNullProvider_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new CognitoUserPoolClient(null!, config));
        Assert.Equal("provider", exception.ParamName);
    }

    [Fact]
    public void GivenNullConfig_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new CognitoUserPoolClient(mockProvider.Object, null!));
        Assert.Equal("cognitoConfig", exception.ParamName);
    }

    [Fact]
    public void GivenConfig_WhenCreateProvider_ThenUsesConfiguredRegion()
    {
        // Act
        using var provider = (AmazonCognitoIdentityProviderClient)CognitoUserPoolClient.CreateProvider(config);

        // Assert
        Assert.Equal("eu-west-2", provider.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public void GivenNullConfig_WhenCreateProvider_ThenThrows()
    {
        Assert.Throws<ArgumentNullException>(() => CognitoUserPoolClient.CreateProvider(null!));
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

    private CognitoUserPoolClient CreateSut() => new(mockProvider.Object, config);
}
