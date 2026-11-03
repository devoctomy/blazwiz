using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Models;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Aws;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public sealed class CognitoAwsCredentialsProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ICognitoIdentityClient> mockIdentity = new();
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly AwsCognitoConfig cognitoConfig = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client", IdentityPoolId = "eu-west-2:pool" };
    private readonly List<CognitoAwsCredentialsProvider> created = [];
    private DateTimeOffset now = Now;

    public CognitoAwsCredentialsProviderTests()
    {
        mockClock.Setup(x => x.GetUtcNow()).Returns(() => now);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = "token" });
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => NewCredentials());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new CognitoAwsCredentialsProvider(null!, mockIdentity.Object, cognitoConfig, offlineMode, mockClock.Object)).ParamName);
        Assert.Equal("identityClient", Assert.Throws<ArgumentNullException>(() => new CognitoAwsCredentialsProvider(mockAuth.Object, null!, cognitoConfig, offlineMode, mockClock.Object)).ParamName);
        Assert.Equal("cognitoConfig", Assert.Throws<ArgumentNullException>(() => new CognitoAwsCredentialsProvider(mockAuth.Object, mockIdentity.Object, null!, offlineMode, mockClock.Object)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new CognitoAwsCredentialsProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, null!, mockClock.Object)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new CognitoAwsCredentialsProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, offlineMode, null!)).ParamName);
    }

    [Fact]
    public async Task GivenCredentialsHeld_WhenOfflineModeOn_ThenNoneHandedOutAndNoAwsCall()
    {
        // Arrange
        var sut = CreateSut();
        await sut.GetCredentialsAsync();
        mockIdentity.Invocations.Clear();
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Act
        var result = await sut.GetCredentialsAsync();

        // Assert
        Assert.Null(result);
        mockIdentity.VerifyNoOtherCalls();
        mockAuth.Verify(x => x.GetCurrentUserAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenPlaceholderSignIn_WhenGetCredentialsAsync_ThenNullWithoutCallingCognito()
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(true);
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetCredentialsAsync());
        mockIdentity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNoIdentityPool_WhenGetCredentialsAsync_ThenNull()
    {
        // Arrange
        cognitoConfig.IdentityPoolId = string.Empty;
        var sut = CreateSut();

        // Act & Assert
        Assert.False(sut.IsConfigured);
        Assert.Null(await sut.GetCredentialsAsync());
        mockIdentity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetCredentialsAsync_ThenNull()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetCredentialsAsync());
    }

    [Fact]
    public async Task GivenSignedIn_WhenGetCredentialsAsync_ThenFetchedOnceAndHeldUntilNearExpiry()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.GetCredentialsAsync();
        now = Now.AddMinutes(54);
        var held = await sut.GetCredentialsAsync();
        now = Now.AddMinutes(56);
        var refreshed = await sut.GetCredentialsAsync();

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, held);
        Assert.NotSame(first, refreshed);
        mockIdentity.Verify(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenConcurrentCallers_WhenGetCredentialsAsync_ThenCognitoAskedOnce()
    {
        // Arrange
        var release = new TaskCompletionSource<AwsTemporaryCredentials>();
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSut();

        // Act
        var first = sut.GetCredentialsAsync();
        var second = sut.GetCredentialsAsync();
        release.SetResult(NewCredentials());
        var results = await Task.WhenAll(first, second);

        // Assert
        Assert.Same(results[0], results[1]);
        mockIdentity.Verify(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenHeldCredentials_WhenCleared_ThenNewCredentialsFetched()
    {
        // Arrange
        var sut = CreateSut();
        var first = await sut.GetCredentialsAsync();

        // Act
        sut.Clear();
        var second = await sut.GetCredentialsAsync();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GivenHeldCredentials_WhenDifferentUserSignsIn_ThenNewCredentialsFetched()
    {
        // Arrange
        var sut = CreateSut();
        var first = await sut.GetCredentialsAsync();
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "alice", IdToken = "token" });

        // Act
        var second = await sut.GetCredentialsAsync();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GivenHeldCredentials_WhenSessionEnds_ThenForgotten()
    {
        // Arrange
        var sut = CreateSut();
        var first = await sut.GetCredentialsAsync();
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        Assert.Null(await sut.GetCredentialsAsync());
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IdToken = "token" });

        // Act
        var second = await sut.GetCredentialsAsync();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GivenIdentityPoolRefusesToken_WhenGetCredentialsAsync_ThenCognitoExceptionPropagatesAndNothingHeld()
    {
        // Arrange
        mockIdentity.SetupSequence(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CognitoException("NotAuthorizedException", "no"))
            .ReturnsAsync(NewCredentials());
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<CognitoException>(() => sut.GetCredentialsAsync());
        Assert.NotNull(await sut.GetCredentialsAsync());
    }

    [Fact]
    public async Task GivenNoNetwork_WhenGetCredentialsAsync_ThenHttpRequestExceptionPropagates()
    {
        // Arrange
        mockIdentity.Setup(x => x.GetCredentialsAsync("token", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetCredentialsAsync());
    }

    public void Dispose()
    {
        foreach (var sut in created)
        {
            sut.Dispose();
        }
    }

    private static AwsTemporaryCredentials NewCredentials() =>
        new() { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t", Expiration = Now.AddHours(1) };

    private CognitoAwsCredentialsProvider CreateSut()
    {
        var sut = new CognitoAwsCredentialsProvider(mockAuth.Object, mockIdentity.Object, cognitoConfig, offlineMode, mockClock.Object);
        created.Add(sut);
        return sut;
    }
}
