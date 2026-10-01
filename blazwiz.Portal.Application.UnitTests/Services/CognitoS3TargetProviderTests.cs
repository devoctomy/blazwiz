using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Aws;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public sealed class CognitoS3TargetProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ICognitoIdentityClient> mockIdentity = new();
    private readonly Mock<ISystemClock> mockClock = new();
    private readonly AwsCognitoConfig cognitoConfig = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client", IdentityPoolId = "eu-west-2:pool" };
    private readonly AwsS3Config s3Config = new() { Region = "eu-west-2", DataBucketName = "data" };
    private readonly BufferingHttpHandler handler = new(new HttpClientHandler());
    private readonly BrowserHttpClientFactory httpClientFactory;
    private DateTimeOffset now = Now;

    public CognitoS3TargetProviderTests()
    {
        httpClientFactory = new BrowserHttpClientFactory(handler);
        mockClock.SetupGet(x => x.UtcNow).Returns(() => now);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = "token" });
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwsTemporaryCredentials { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t", Expiration = Now.AddHours(1) });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(null!, mockIdentity.Object, cognitoConfig, s3Config, httpClientFactory, mockClock.Object)).ParamName);
        Assert.Equal("identityClient", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockAuth.Object, null!, cognitoConfig, s3Config, httpClientFactory, mockClock.Object)).ParamName);
        Assert.Equal("cognitoConfig", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockAuth.Object, mockIdentity.Object, null!, s3Config, httpClientFactory, mockClock.Object)).ParamName);
        Assert.Equal("s3Config", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, null!, httpClientFactory, mockClock.Object)).ParamName);
        Assert.Equal("httpClientFactory", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, s3Config, null!, mockClock.Object)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new CognitoS3TargetProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, s3Config, httpClientFactory, null!)).ParamName);
    }

    [Fact]
    public async Task GivenPlaceholderSignIn_WhenGetTargetAsync_ThenNullWithoutCallingCognito()
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(true);
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetTargetAsync());
        mockIdentity.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", "data")]
    [InlineData("eu-west-2:pool", "")]
    public async Task GivenIdentityPoolOrBucketMissing_WhenGetTargetAsync_ThenNull(string identityPool, string bucket)
    {
        // Arrange
        cognitoConfig.IdentityPoolId = identityPool;
        s3Config.DataBucketName = bucket;
        using var sut = CreateSut();

        // Act & Assert
        Assert.False(sut.IsConfigured);
        Assert.Null(await sut.GetTargetAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetTargetAsync_ThenNull()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetTargetAsync());
    }

    [Fact]
    public async Task GivenSignedIn_WhenGetTargetAsync_ThenBucketAndIdentityPrefixAndCachedUntilNearExpiry()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var first = await sut.GetTargetAsync();
        now = Now.AddMinutes(50);
        var cached = await sut.GetTargetAsync();
        now = Now.AddMinutes(56);
        var refreshed = await sut.GetTargetAsync();

        // Assert
        Assert.Equal("data", first!.Bucket);
        Assert.Equal("eu-west-2:id/", first.RootPrefix);
        Assert.Same(first, cached);
        Assert.NotSame(first, refreshed);
        mockIdentity.Verify(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenCachedTarget_WhenInvalidated_ThenNewCredentialsFetched()
    {
        // Arrange
        using var sut = CreateSut();
        var first = await sut.GetTargetAsync();

        // Act
        sut.Invalidate();
        var second = await sut.GetTargetAsync();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GivenIdentityPoolRefusesToken_WhenGetTargetAsync_ThenNull()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>())).ThrowsAsync(new CognitoException("NotAuthorizedException", "no"));
        using var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetTargetAsync());
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetTargetAsync_ThenHttpRequestExceptionPropagates()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetTargetAsync());
    }

    public void Dispose()
    {
        httpClientFactory.Dispose();
        handler.Dispose();
    }

    private CognitoS3TargetProvider CreateSut() =>
        new(mockAuth.Object, mockIdentity.Object, cognitoConfig, s3Config, httpClientFactory, mockClock.Object);
}
