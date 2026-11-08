using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class SecretsManagerUserKeysStoreTests
{
    private const string Sub = "11111111-2222-3333-4444-555555555555";
    private const string SecretName = Sub + "/keys/s0";

    private static readonly string IdToken = new JsonWebTokenHandler().CreateToken($$"""{"sub":"{{Sub}}"}""");

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IAwsCredentialsProvider> mockCredentials = new();
    private readonly Mock<ISecretsManagerClientFactory> mockFactory = new();
    private readonly Mock<IAmazonSecretsManager> mockClient = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly AwsTemporaryCredentials credentials = new() { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t" };
    private readonly BlazwizAppOptions app = new() { Name = "myapp", Id = "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8" };

    public SecretsManagerUserKeysStoreTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = IdToken });
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        mockFactory.Setup(x => x.Create(credentials)).Returns(mockClient.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(null!, mockCredentials.Object, mockFactory.Object, offlineMode, app)).ParamName);
        Assert.Equal("credentialsProvider", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, null!, mockFactory.Object, offlineMode, app)).ParamName);
        Assert.Equal("clientFactory", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, mockCredentials.Object, null!, offlineMode, app)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, mockCredentials.Object, mockFactory.Object, null!, app)).ParamName);
        Assert.Equal("app", Assert.Throws<ArgumentNullException>(() => new SecretsManagerUserKeysStore(mockAuth.Object, mockCredentials.Object, mockFactory.Object, offlineMode, null!)).ParamName);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenGetAsync_ThenThrowsUnreachableNotUnsupportedWithoutCallingAws()
    {
        // Arrange (unsupported would turn encryption off here; offline the keys are only out of reach)
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync(new E2eKeyScope("s0")));
        mockCredentials.VerifyNoOtherCalls();
        mockFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenPlaceholderSignIn_WhenGetAsync_ThenThrowsInvalidOperationExceptionWithoutCallingAws()
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(true);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync(new E2eKeyScope("s0")));
        mockCredentials.VerifyNoOtherCalls();
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
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync(new E2eKeyScope("s0")));
        mockCredentials.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenIdTokenWithoutSub_WhenGetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = new JsonWebTokenHandler().CreateToken("""{"email":"bob@example.com"}""") });
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync(new E2eKeyScope("s0")));
    }

    [Fact]
    public async Task GivenSignedOut_WhenSetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetAsync(new E2eKeyScope("s0"), "keys"));
    }

    [Fact]
    public async Task GivenNoCredentials_WhenGetAsync_ThenThrowsInvalidOperationExceptionWithoutCallingAws()
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AwsTemporaryCredentials?)null);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync(new E2eKeyScope("s0")));
        mockFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenStoredKeys_WhenGetAsync_ThenReturnsValueFromSecretNamedAfterSub()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == SecretName), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "keys" });
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync(new E2eKeyScope("s0"));

        // Assert
        Assert.Equal("keys", result);
        mockClient.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GivenAnotherScope_WhenGetAsync_ThenReadsTheScopesOwnSecret()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == Sub + "/keys/s1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "scope keys" });
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync(new E2eKeyScope("s1"));

        // Assert
        Assert.Equal("scope keys", result);
    }

    [Fact]
    public async Task GivenTheIndex_WhenGetIndexAsync_ThenReadsTheIndexSecret()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == Sub + "/keys/index"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "index" });
        var sut = CreateSut();

        // Act
        var result = await sut.GetIndexAsync();

        // Assert
        Assert.Equal("index", result);
    }

    [Fact]
    public async Task GivenAnIndex_WhenSetIndexAsync_ThenWritesTheIndexSecret()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetIndexAsync("index");

        // Assert
        mockClient.Verify(x => x.PutSecretValueAsync(It.Is<PutSecretValueRequest>(r => r.SecretId == Sub + "/keys/index" && r.SecretString == "index"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNullScope_WhenUsed_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteAsync(null!));
    }

    [Fact]
    public async Task GivenNoSecret_WhenGetAsync_ThenReturnsNull()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetAsync(new E2eKeyScope("s0")));
    }

    [Fact]
    public async Task GivenExistingSecret_WhenSetAsync_ThenPutsNewValue()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetAsync(new E2eKeyScope("s0"), "keys");

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
        await sut.SetAsync(new E2eKeyScope("s0"), "keys");

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
        await sut.SetAsync(new E2eKeyScope("s0"), "keys");

        // Assert
        mockClient.Verify(x => x.PutSecretValueAsync(It.Is<PutSecretValueRequest>(r => r.SecretId == SecretName && r.SecretString == "keys"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenNullValue_WhenSetAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(new E2eKeyScope("s0"), null!));
    }

    [Fact]
    public async Task GivenNoSecret_WhenTryCreateAsync_ThenCreatesSecretAndReturnsTrue()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var created = await sut.TryCreateAsync(new E2eKeyScope("s0"), "keys");

        // Assert
        Assert.True(created);
        mockClient.Verify(x => x.CreateSecretAsync(It.Is<CreateSecretRequest>(r => r.Name == SecretName && r.SecretString == "keys"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenExistingSecret_WhenTryCreateAsync_ThenReturnsFalseWithoutReplacingIt()
    {
        // Arrange
        mockClient.Setup(x => x.CreateSecretAsync(It.IsAny<CreateSecretRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceExistsException("exists"));
        var sut = CreateSut();

        // Act
        var created = await sut.TryCreateAsync(new E2eKeyScope("s0"), "keys");

        // Assert
        Assert.False(created);
        mockClient.Verify(x => x.PutSecretValueAsync(It.IsAny<PutSecretValueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNullValue_WhenTryCreateAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryCreateAsync(new E2eKeyScope("s0"), null!));
    }

    [Fact]
    public async Task GivenPlaceholderSignIn_WhenTryCreateAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(true);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.TryCreateAsync(new E2eKeyScope("s0"), "keys"));
    }

    [Fact]
    public async Task GivenSecret_WhenDeleteAsync_ThenDeletesWithARecoveryWindow()
    {
        // Arrange: never at once, so a bug or a hijacked session cannot destroy a profile's keys for good.
        var sut = CreateSut();

        // Act
        await sut.DeleteAsync(new E2eKeyScope("s0"));

        // Assert
        mockClient.Verify(
            x => x.DeleteSecretAsync(
                It.Is<DeleteSecretRequest>(r => r.SecretId == SecretName && r.RecoveryWindowInDays == SecretsManagerUserKeysStore.RecoveryWindowDays && r.ForceDeleteWithoutRecovery != true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenNoSecret_WhenDeleteAsync_ThenDoesNothing()
    {
        // Arrange
        mockClient.Setup(x => x.DeleteSecretAsync(It.IsAny<DeleteSecretRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act & Assert
        await sut.DeleteAsync(new E2eKeyScope("s0"));
    }

    [Fact]
    public async Task GivenIdentityPoolRefusesToken_WhenGetAsync_ThenCognitoExceptionPropagates()
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "no"));
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<CognitoException>(() => sut.GetAsync(new E2eKeyScope("s0")));
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetAsync_ThenThrowsHttpRequestException()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("offline", new HttpRequestException()));
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync(new E2eKeyScope("s0")));
    }

    [Fact]
    public async Task GivenAccessDenied_WhenGetAsync_ThenAwsExceptionPropagates()
    {
        // Arrange
        mockClient.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSecretsManagerException("denied") { ErrorCode = "AccessDeniedException" });
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<AmazonSecretsManagerException>(() => sut.GetAsync(new E2eKeyScope("s0")));
    }

    [Fact]
    public async Task GivenAnApp_WhenASecretIsCreated_ThenItIsTaggedWithTheAppsName()
    {
        // Arrange: the tag is how remove.ps1 finds the users' secrets when the environment goes.
        mockClient.Setup(x => x.PutSecretValueAsync(It.IsAny<PutSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));
        var sut = CreateSut();

        // Act
        await sut.TryCreateAsync(new E2eKeyScope("s0"), "keys");
        await sut.SetIndexAsync("index");

        // Assert
        mockClient.Verify(
            x => x.CreateSecretAsync(It.Is<CreateSecretRequest>(r => r.Tags.Count == 1 && r.Tags[0].Key == SecretsManagerUserKeysStore.AppTag && r.Tags[0].Value == "myapp"), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private SecretsManagerUserKeysStore CreateSut() =>
        new(mockAuth.Object, mockCredentials.Object, mockFactory.Object, offlineMode, app);
}
