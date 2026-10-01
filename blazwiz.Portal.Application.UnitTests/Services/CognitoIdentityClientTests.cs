using System.Net;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.Runtime;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Aws;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class CognitoIdentityClientTests
{
    private const string Login = "cognito-idp.eu-west-2.amazonaws.com/eu-west-2_Pool";

    private readonly Mock<IAmazonCognitoIdentity> mockIdentity = new();
    private readonly AwsCognitoConfig config = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", IdentityPoolId = "eu-west-2:pool" };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
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
    public async Task GivenNoToken_WhenGetCredentialsAsync_ThenThrows()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new CognitoIdentityClient(mockIdentity.Object, config).GetCredentialsAsync(string.Empty));
    }
}
