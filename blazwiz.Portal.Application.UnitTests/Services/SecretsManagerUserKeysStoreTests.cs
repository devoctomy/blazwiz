using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Aws;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class SecretsManagerUserKeysStoreTests
{
    private const string Sub = "11111111-2222-3333-4444-555555555555";
    private const string SecretName = Sub + "/keys";

    private static readonly string IdToken = new JsonWebTokenHandler().CreateToken($$"""{"sub":"{{Sub}}"}""");

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ICognitoIdentityClient> mockIdentity = new();
    private readonly Mock<ISecretsManagerClientFactory> mockFactory = new();
    private readonly Mock<IAmazonSecretsManager> mockClient = new();
    private readonly AwsTemporaryCredentials credentials = new() { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t" };

    public SecretsManagerUserKeysStoreTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = IdToken });
        mockIdentity.Setup(x => x.GetCredentialsAsync(IdToken, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        mockFactory.Setup(x => x.Create(credentials)).Returns(mockClient.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(null!, mockIdentity.Object, mockFactory.Object)).ParamName);
        Assert.Equal("identityClient", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, null!, mockFactory.Object)).ParamName);
        Assert.Equal("clientFactory", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, mockIdentity.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenPlaceholderSignIn_WhenGetAsync_ThenThrowsInvalidOperationExceptionWithoutCallingAws()
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(true);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync());
        mockIdentity.VerifyNoOtherCalls();
        mockFactory.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a jwt")]
    public async Task GivenNoUsableIdToken_WhenGetAsync_ThenThrowsInvalidOperationException(string? idToken)
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = idToken });
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync());
        mockIdentity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenIdTokenWithoutSub_WhenGetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = new JsonWebTokenHandler().CreateToken("""{"email":"bob@example.com"}""") });
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenSetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetAsync("keys"));
    }

    [Fact]
    public async Task GivenStoredKeys_WhenGetAsync_ThenReturnsValueFromSecretNamedAfterSub()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == SecretName), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "keys" });
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync();

        // Assert
        Assert.Equal("keys", result);
        mockClient.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GivenNoSecret_WhenGetAsync_ThenReturnsNull()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetAsync());
    }

    [Fact]
    public async Task GivenExistingSecret_WhenSetAsync_ThenPutsNewValue()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetAsync("keys");

        // Assert
        mockClient.Verify(x => x.PutSecretValueAsync(It.Is<PutSecretValueRequest>(r => r.SecretId == SecretName && r.SecretString == "keys"), It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(x => x.CreateSecretAsync(It.IsAny<CreateSecretRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoSecret_WhenSetAsync_ThenCreatesSecret()
    {
        // Arrange
        mockClient.Setup(x => x.PutSecretValueAsync(It.IsAny<PutSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act
        await sut.SetAsync("keys");

        // Assert
        mockClient.Verify(x => x.CreateSecretAsync(It.Is<CreateSecretRequest>(r => r.Name == SecretName && r.SecretString == "keys"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenSecretCreatedInBetween_WhenSetAsync_ThenPutsValueAgain()
    {
        // Arrange
        mockClient.SetupSequence(x => x.PutSecretValueAsync(It.IsAny<PutSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"))
            .ReturnsAsync(new PutSecretValueResponse());
        mockClient.Setup(x => x.CreateSecretAsync(It.IsAny<CreateSecretRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceExistsException("exists"));
        var sut = CreateSut();

        // Act
        await sut.SetAsync("keys");

        // Assert
        mockClient.Verify(x => x.PutSecretValueAsync(It.Is<PutSecretValueRequest>(r => r.SecretId == SecretName && r.SecretString == "keys"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenNullValue_WhenSetAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!));
    }

    [Fact]
    public async Task GivenSecret_WhenDeleteAsync_ThenDeletesWithoutRecoveryWindow()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.DeleteAsync();

        // Assert
        mockClient.Verify(x => x.DeleteSecretAsync(It.Is<DeleteSecretRequest>(r => r.SecretId == SecretName && r.ForceDeleteWithoutRecovery == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoSecret_WhenDeleteAsync_ThenDoesNothing()
    {
        // Arrange
        mockClient.Setup(x => x.DeleteSecretAsync(It.IsAny<DeleteSecretRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act & Assert
        await sut.DeleteAsync();
    }

    [Fact]
    public async Task GivenIdentityPoolRefusesToken_WhenGetAsync_ThenCognitoExceptionPropagates()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetCredentialsAsync(IdToken, It.IsAny<CancellationToken>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "no"));
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<CognitoException>(() => sut.GetAsync());
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetAsync_ThenThrowsHttpRequestException()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("offline", new HttpRequestException()));
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync());
    }

    [Fact]
    public async Task GivenAccessDenied_WhenGetAsync_ThenAwsExceptionPropagates()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSecretsManagerException("denied") { ErrorCode = "AccessDeniedException" });
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<AmazonSecretsManagerException>(() => sut.GetAsync());
    }

    private SecretsManagerUserKeysStore CreateSut() =>
        new(mockAuth.Object, mockIdentity.Object, mockFactory.Object);
}
