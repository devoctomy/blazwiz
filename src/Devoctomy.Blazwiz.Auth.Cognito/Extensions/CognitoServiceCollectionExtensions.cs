using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Auth.Cognito.Extensions;

/// <summary>
/// Extension methods for signing in with AWS Cognito.
/// </summary>
public static class CognitoServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section holding the Cognito settings (<see cref="AwsCognitoConfig"/>).
    /// </summary>
    public const string ConfigurationSection = "AwsCognito";

    /// <summary>
    /// Signs in with the Cognito user pool in the <c>AwsCognito</c> configuration section, and gets the session's AWS
    /// credentials from its identity pool. When no user pool is configured, the placeholder sign in (any credentials)
    /// is kept only where <paramref name="allowPlaceholderSignIn"/> says so: pass the host environment's
    /// <c>IsDevelopment()</c>, so a deployed app whose settings are missing refuses to start rather than letting anyone
    /// in. Needs <c>AddBlazwizApp</c> first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration.</param>
    /// <param name="allowPlaceholderSignIn">Whether the placeholder sign in may stand in for Cognito when it is not configured.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Cognito is not configured and the placeholder is not allowed.</exception>
    public static IServiceCollection AddBlazwizCognito(this IServiceCollection services, IConfiguration configuration, bool allowPlaceholderSignIn)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBlazwizAuth();

        var cognitoConfig = new AwsCognitoConfig();
        configuration.GetSection(ConfigurationSection).Bind(cognitoConfig);
        if (!CognitoAuthenticationService.IsConfigured(cognitoConfig) && !allowPlaceholderSignIn)
        {
            throw new InvalidOperationException(
                $"The {ConfigurationSection} settings (Region, UserPoolId and UserPoolClientId) are missing, and the placeholder sign in is not allowed here. " +
                "Deploy with cloudformation/deploy.ps1, which writes them to appsettings.Production.json, or allow the placeholder for local development only.");
        }

        services.AddSingleton(cognitoConfig);

        // The Cognito sign-in APIs are public, so the API client uses no AWS credentials.
        services.TryAddSingleton(_ => new BufferingHttpHandler(new HttpClientHandler()));
        services.TryAddSingleton<BrowserHttpClientFactory>();
        services.TryAddSingleton<IAmazonCognitoIdentityProvider>(sp => new AmazonCognitoIdentityProviderClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityProviderConfig
            {
                RegionEndpoint = GetRegion(sp.GetRequiredService<AwsCognitoConfig>().Region),
                HttpClientFactory = sp.GetRequiredService<BrowserHttpClientFactory>(),
            }));
        services.TryAddScoped<ICognitoUserPoolClient, CognitoUserPoolClient>();

        // The identity pool's GetId / GetCredentialsForIdentity are public APIs, so no credentials.
        services.TryAddSingleton<IAmazonCognitoIdentity>(sp => new AmazonCognitoIdentityClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityConfig
            {
                RegionEndpoint = GetRegion(sp.GetRequiredService<AwsCognitoConfig>().Region),
                HttpClientFactory = sp.GetRequiredService<BrowserHttpClientFactory>(),
            }));
        services.TryAddScoped<ICognitoIdentityClient, CognitoIdentityClient>();

        // The session's AWS credentials, fetched once signed in, held in memory and shared by everything calling AWS.
        services.TryAddScoped<IAwsCredentialsProvider, CognitoAwsCredentialsProvider>();
        services.AddSessionLifecycleHandler<AwsCredentialsSessionHandler>();

        // Cognito when a user pool is configured (deployed), otherwise the placeholder sign in and password changes
        // (local development).
        services.Replace(ServiceDescriptor.Scoped<IAuthenticationService>(sp =>
            CognitoAuthenticationService.IsConfigured(sp.GetService<AwsCognitoConfig>())
                ? ActivatorUtilities.CreateInstance<CognitoAuthenticationService>(sp)
                : ActivatorUtilities.CreateInstance<LocalAuthenticationService>(sp)));
        services.Replace(ServiceDescriptor.Scoped<IPasswordService>(sp =>
            CognitoAuthenticationService.IsConfigured(sp.GetService<AwsCognitoConfig>())
                ? ActivatorUtilities.CreateInstance<CognitoPasswordService>(sp)
                : ActivatorUtilities.CreateInstance<LocalPasswordService>(sp)));
        return services;
    }

    // Used before the settings are known to be complete; the clients are only called once they are.
    private static RegionEndpoint GetRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? RegionEndpoint.USEast1 : RegionEndpoint.GetBySystemName(region);
}
