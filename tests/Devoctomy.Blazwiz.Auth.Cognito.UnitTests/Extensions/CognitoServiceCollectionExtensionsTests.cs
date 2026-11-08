using Devoctomy.Blazwiz.TestSupport;
using Amazon.CognitoIdentityProvider;
using Devoctomy.Blazwiz.Auth.Cognito.Extensions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Extensions;

public class CognitoServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizCognito_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CognitoServiceCollectionExtensions.AddBlazwizCognito(null!, Configuration(), true));
        Assert.Throws<ArgumentNullException>(() => TestServices.Create().AddBlazwizCognito(null!, true));
    }

    [Fact]
    public async Task GivenUserPoolConfigured_WhenAddBlazwizCognito_ThenSignsInWithCognito()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        var result = services.AddBlazwizCognito(Configuration(configured: true), allowPlaceholderSignIn: false);

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<CognitoAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<CognitoPasswordService>(scope.ServiceProvider.GetRequiredService<IPasswordService>());
        Assert.IsType<CognitoUserPoolClient>(scope.ServiceProvider.GetRequiredService<ICognitoUserPoolClient>());
        Assert.IsType<CognitoIdentityClient>(scope.ServiceProvider.GetRequiredService<ICognitoIdentityClient>());
        Assert.IsType<CognitoAwsCredentialsProvider>(scope.ServiceProvider.GetRequiredService<IAwsCredentialsProvider>());
        Assert.Single(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>(), h => h is AwsCredentialsSessionHandler);
    }

    [Fact]
    public async Task GivenNoUserPool_WhenAddBlazwizCognito_ThenKeepsThePlaceholderSignIn()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizCognito(Configuration(), allowPlaceholderSignIn: true);

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<LocalPasswordService>(scope.ServiceProvider.GetRequiredService<IPasswordService>());
        Assert.Single(services, d => d.ServiceType == typeof(IAuthenticationService));
        Assert.Single(services, d => d.ServiceType == typeof(IPasswordService));
    }

    [Fact]
    public void GivenNoUserPoolAndNoPlaceholderAllowed_WhenAddBlazwizCognito_ThenThrows()
    {
        // Arrange: a deployed app whose settings went missing must refuse to start, not let anyone in.
        var services = TestServices.Create();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddBlazwizCognito(Configuration(), allowPlaceholderSignIn: false));
        Assert.Contains("AwsCognito", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenConfiguration_WhenAddBlazwizCognito_ThenSettingsAreBound()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizCognito(Configuration(configured: true), allowPlaceholderSignIn: false);

        // Assert
        using var provider = services.BuildServiceProvider();
        var cognito = provider.GetRequiredService<AwsCognitoConfig>();
        Assert.Equal("eu-west-2", cognito.Region);
        Assert.Equal("eu-west-2_Pool", cognito.UserPoolId);
        Assert.Equal("client", cognito.UserPoolClientId);
        Assert.Equal("identity", cognito.IdentityPoolId);
    }

    [Fact]
    public void GivenUserPoolConfigured_WhenAddBlazwizCognito_ThenApiClientUsesRegionAndBrowserHttpClient()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizCognito(Configuration(configured: true), allowPlaceholderSignIn: false);

        // Assert
        using var provider = services.BuildServiceProvider();
        var client = Assert.IsType<AmazonCognitoIdentityProviderClient>(provider.GetRequiredService<IAmazonCognitoIdentityProvider>());
        Assert.Equal("eu-west-2", client.Config.RegionEndpoint.SystemName);
        Assert.Same(provider.GetRequiredService<BrowserHttpClientFactory>(), client.Config.HttpClientFactory);
        Assert.Same(client, provider.GetRequiredService<IAmazonCognitoIdentityProvider>());
    }

    private static IConfiguration Configuration(bool configured = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configured
                ? new Dictionary<string, string?>
                {
                    ["AwsCognito:Region"] = "eu-west-2",
                    ["AwsCognito:UserPoolId"] = "eu-west-2_Pool",
                    ["AwsCognito:UserPoolClientId"] = "client",
                    ["AwsCognito:IdentityPoolId"] = "identity",
                }
                : [])
            .Build();
}
