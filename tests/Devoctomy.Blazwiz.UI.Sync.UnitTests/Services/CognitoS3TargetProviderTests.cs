using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public sealed class CognitoS3TargetProviderTests : IDisposable
{
    private readonly Mock<IAwsCredentialsProvider> mockCredentials = new();
    private readonly AwsS3Config s3Config = new() { Region = "eu-west-2", DataBucketName = "data" };
    private readonly BufferingHttpHandler handler = new(new HttpClientHandler());
    private readonly BrowserHttpClientFactory httpClientFactory;
    private AwsTemporaryCredentials? credentials = NewCredentials();

    public CognitoS3TargetProviderTests()
    {
        httpClientFactory = new BrowserHttpClientFactory(handler);
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => credentials);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("credentialsProvider", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(null!, s3Config, httpClientFactory)).ParamName);
        Assert.Equal("s3Config", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockCredentials.Object, null!, httpClientFactory)).ParamName);
        Assert.Equal("httpClientFactory", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockCredentials.Object, s3Config, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNoBucket_WhenGetTargetAsync_ThenNullWithoutGettingCredentials()
    {
        // Arrange
        s3Config.DataBucketName = string.Empty;
        using var sut = CreateSut();

        // Act & Assert
        Assert.False(sut.IsConfigured);
        Assert.Null(await sut.GetTargetAsync());
        mockCredentials.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNoCredentials_WhenGetTargetAsync_ThenNull()
    {
        // Arrange
        credentials = null;
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetTargetAsync());
    }

    [Fact]
    public async Task GivenCredentials_WhenGetTargetAsync_ThenBucketAndIdentityPrefixKeptUntilCredentialsChange()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var first = await sut.GetTargetAsync();
        var kept = await sut.GetTargetAsync();
        credentials = NewCredentials();
        var renewed = await sut.GetTargetAsync();

        // Assert
        Assert.Equal("data", first!.Bucket);
        Assert.Equal("eu-west-2:id/", first.RootPrefix);
        Assert.Same(first, kept);
        Assert.NotSame(first, renewed);
    }

    [Fact]
    public async Task GivenTarget_WhenInvalidated_ThenSharedCredentialsClearedAndNewTargetBuilt()
    {
        // Arrange
        using var sut = CreateSut();
        var first = await sut.GetTargetAsync();

        // Act
        sut.Invalidate();
        var second = await sut.GetTargetAsync();

        // Assert
        mockCredentials.Verify(x => x.Clear(), Times.Once);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GivenIdentityPoolRefusesToken_WhenGetTargetAsync_ThenNull()
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "no"));
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetTargetAsync());
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetTargetAsync_ThenHttpRequestExceptionPropagates()
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetTargetAsync());
    }

    public void Dispose()
    {
        httpClientFactory.Dispose();
        handler.Dispose();
    }

    private static AwsTemporaryCredentials NewCredentials() =>
        new() { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t", Expiration = DateTimeOffset.UtcNow.AddHours(1) };

    private CognitoS3TargetProvider CreateSut() =>
        new(mockCredentials.Object, s3Config, httpClientFactory);
}
