using Amazon.SecretsManager;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.UI.Sync.Services;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public sealed class SecretsManagerClientFactoryTests : IDisposable
{
    private readonly AwsCognitoConfig cognitoConfig = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client", IdentityPoolId = "eu-west-2:pool" };
    private readonly BufferingHttpHandler handler = new(new HttpClientHandler());
    private readonly BrowserHttpClientFactory httpClientFactory;

    public SecretsManagerClientFactoryTests()
    {
        httpClientFactory = new BrowserHttpClientFactory(handler);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cognitoConfig", Assert.Throws<ArgumentNullException>(() => new SecretsManagerClientFactory(null!, httpClientFactory)).ParamName);
        Assert.Equal("httpClientFactory", Assert.Throws<ArgumentNullException>(() => new SecretsManagerClientFactory(cognitoConfig, null!)).ParamName);
    }

    [Fact]
    public void GivenNullCredentials_WhenCreate_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new SecretsManagerClientFactory(cognitoConfig, httpClientFactory);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
    }

    [Fact]
    public void GivenCredentials_WhenCreate_ThenClientUsesRegionAndBrowserHttpClient()
    {
        // Arrange
        var sut = new SecretsManagerClientFactory(cognitoConfig, httpClientFactory);

        // Act
        using var client = sut.Create(new AwsTemporaryCredentials { IdentityId = "eu-west-2:id", AccessKeyId = "AKIA", SecretKey = "s", SessionToken = "t" });

        // Assert
        var concrete = Assert.IsType<AmazonSecretsManagerClient>(client);
        Assert.Equal("eu-west-2", concrete.Config.RegionEndpoint.SystemName);
        Assert.Same(httpClientFactory, concrete.Config.HttpClientFactory);
    }

    public void Dispose()
    {
        httpClientFactory.Dispose();
        handler.Dispose();
    }
}
