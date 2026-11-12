using System.Net;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class CognitoIdentityClientTests
{
    private const string Login = "cognito-idp.eu-west-2.amazonaws.com/eu-west-2_Pool";

    private readonly Mock<IAmazonCognitoIdentity> mockIdentity = new();
    private readonly AwsCognitoConfig config = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", IdentityPoolId = "eu-west-2:pool" };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("identity", Assert.Throws<ArgumentNullException>(() => new CognitoIdentityClient(null!, config)).ParamName);
        Assert.Equal("cognitoConfig", Assert.Throws<ArgumentNullException>(() => new CognitoIdentityClient(mockIdentity.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenIdToken_WhenGetCredentialsAsync_ThenIdentityAndTemporaryCredentialsReturned()
    {
        // Arrange
        var expiration = new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc);
        mockIdentity.Setup(x => x.GetIdAsync(It.Is<GetIdRequest>(r => r.IdentityPoolId == "eu-west-2:pool" && r.Logins[Login] == "token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetIdResponse { IdentityId = "eu-west-2:0a1b2c3d-0000-4000-8000-000000000001" });
        mockIdentity.Setup(x => x.GetCredentialsForIdentityAsync(It.Is<GetCredentialsForIdentityRequest>(r => r.IdentityId == "eu-west-2:0a1b2c3d-0000-4000-8000-000000000001" && r.Logins[Login] == "token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCredentialsForIdentityResponse
            {
                IdentityId = "eu-west-2:0a1b2c3d-0000-4000-8000-000000000001",
                Credentials = new Credentials { AccessKeyId = "AKIA", SecretKey = "secret", SessionToken = "session", Expiration = expiration },
            });

        // Act
        var result = await new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token");

        // Assert
        Assert.Equal("eu-west-2:0a1b2c3d-0000-4000-8000-000000000001", result.IdentityId);
        Assert.Equal("AKIA", result.AccessKeyId);
        Assert.Equal("secret", result.SecretKey);
        Assert.Equal("session", result.SessionToken);
        Assert.Equal(new DateTimeOffset(expiration), result.Expiration);
    }

    [Fact]
    public async Task GivenTokenRefused_WhenGetCredentialsAsync_ThenCognitoException()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetIdAsync(It.IsAny<GetIdRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Invalid login token.") { ErrorCode = "NotAuthorizedException" });

        // Act
        var ex = await Assert.ThrowsAsync<CognitoException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token"));

        // Assert
        Assert.Equal("NotAuthorizedException", ex.ErrorType);
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetCredentialsAsync_ThenHttpRequestException()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetIdAsync(It.IsAny<GetIdRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("failed", new HttpRequestException(), HttpStatusCode.ServiceUnavailable));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token"));
    }

    [Fact]
    public async Task GivenTheResponseHasNoIdentityOrExpiry_WhenGetCredentialsAsync_ThenTheIdentityFromGetIdIsUsedAndTheyExpireNow()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetIdAsync(It.IsAny<GetIdRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetIdResponse { IdentityId = "eu-west-2:0000-abcd-ef01" });
        mockIdentity.Setup(x => x.GetCredentialsForIdentityAsync(It.IsAny<GetCredentialsForIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCredentialsForIdentityResponse
            {
                Credentials = new Credentials { AccessKeyId = "AKIA", SecretKey = "secret", SessionToken = "session", Expiration = null },
            });
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token");

        // Assert
        var after = DateTimeOffset.UtcNow;
        Assert.Equal("eu-west-2:0000-abcd-ef01", result.IdentityId);
        Assert.Equal("AKIA", result.AccessKeyId);
        Assert.InRange(result.Expiration, before, after);
        Assert.Equal(TimeSpan.Zero, result.Expiration.Offset);
    }

    [Fact]
    public async Task GivenARefusalWithoutAnErrorCode_WhenGetCredentialsAsync_ThenCognitoExceptionNamedAfterItsType()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetIdAsync(It.IsAny<GetIdRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("No such identity pool.") { ErrorCode = null! });

        // Act
        var ex = await Assert.ThrowsAsync<CognitoException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token"));

        // Assert
        Assert.Equal(nameof(ResourceNotFoundException), ex.ErrorType);
    }

    [Fact]
    public async Task GivenAServiceErrorThatIsNotANetworkFailure_WhenGetCredentialsAsync_ThenItPropagatesUnchanged()
    {
        // Arrange
        var failure = new AmazonServiceException("throttled", new InvalidOperationException(), HttpStatusCode.InternalServerError);
        mockIdentity.Setup(x => x.GetIdAsync(It.IsAny<GetIdRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        // Act
        var ex = await Assert.ThrowsAsync<AmazonServiceException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync("token"));

        // Assert
        Assert.Same(failure, ex);
    }

    [Fact]
    public async Task GivenNoToken_WhenGetCredentialsAsync_ThenThrows()
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync(string.Empty));
    }
}
