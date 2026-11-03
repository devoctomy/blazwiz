using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Extensions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;
using blazwiz.Portal.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace blazwiz.Portal.Application.Extensions;

/// <summary>
/// Extension methods for registering the framework's portal services: authentication, theme, settings and the app
/// shell's extension points. Optional modules (sync and encryption) are added with their own methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the framework's portal services to the dependency injection container.
    /// Browser specific services (<see cref="INavigationService"/>, <see cref="IBrowserInteropService"/>, <see cref="IWebAuthnService"/>) are registered by the portal itself.
    /// Call <see cref="AddPortalSettings"/> first so the Cognito settings decide which authentication service is used.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddPortalApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The app's name and ID: everything kept in the browser is named after the app (e.g. blazwiz_session).
        services.AddBlazwizApp(AppIdentity.Name, AppIdentity.Id);

        // The extension points modules and the app add to: shell regions, Settings sections, sidebar, pages.
        services.AddBlazwizShell();

        // Text from resources the app can override and translate, and error descriptions.
        services.AddBlazwizUiText();

        // The clock and timers (e.g. the session countdown). Also added by sync, hence TryAdd.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILocalStorageService, LocalStorageService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IPasswordCryptoService, PasswordCryptoService>();

        // Keys derived from the WebAuthn PRF output, kept in memory; features get them from IDerivedKeyStore by name.
        services.AddBlazwizPrfKeyDerivation(prf => prf
            .UseApplication(AppIdentity.Name, AppIdentity.Id)
            .AddKey(PasswordCryptoService.RememberMeKeyName));
        services.AddSingleton<IUserDisplayService, UserDisplayService>();

        // The Cognito sign-in APIs are public, so the API client uses no AWS credentials.
        services.AddSingleton(_ => new BufferingHttpHandler(new HttpClientHandler()));
        services.AddSingleton<BrowserHttpClientFactory>();
        services.AddSingleton<IAmazonCognitoIdentityProvider>(sp => new AmazonCognitoIdentityProviderClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityProviderConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(sp.GetRequiredService<AwsCognitoConfig>().Region),
                HttpClientFactory = sp.GetRequiredService<BrowserHttpClientFactory>(),
            }));
        services.AddScoped<ICognitoUserPoolClient, CognitoUserPoolClient>();

        // The identity pool's GetId / GetCredentialsForIdentity are public APIs, so no credentials.
        services.AddSingleton<IAmazonCognitoIdentity>(sp => new AmazonCognitoIdentityClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityConfig
            {
                RegionEndpoint = GetRegion(sp.GetRequiredService<AwsCognitoConfig>().Region),
                HttpClientFactory = sp.GetRequiredService<BrowserHttpClientFactory>(),
            }));
        services.AddScoped<ICognitoIdentityClient, CognitoIdentityClient>();

        // The session's AWS credentials, fetched once signed in, held in memory and shared by everything calling AWS.
        services.AddScoped<IAwsCredentialsProvider, CognitoAwsCredentialsProvider>();
        services.AddSessionLifecycleHandler<AwsCredentialsSessionHandler>();

        // The user's keys, in their own Secrets Manager secret named <user pool sub>/keys.
        services.AddSingleton<ISecretsManagerClientFactory, SecretsManagerClientFactory>();
        services.AddScoped<IUserKeysStore, SecretsManagerUserKeysStore>();

        // Cognito when a user pool is configured (deployed), otherwise the placeholder login (local development).
        // The offline session, held in memory and shared by whichever authentication service is used.
        services.AddScoped<IOfflineSessionStore, InMemoryOfflineSessionStore>();
        services.AddScoped<IAuthenticationService>(sp =>
            CognitoAuthenticationService.IsConfigured(sp.GetService<AwsCognitoConfig>())
                ? ActivatorUtilities.CreateInstance<CognitoAuthenticationService>(sp)
                : ActivatorUtilities.CreateInstance<LocalAuthenticationService>(sp));

        return services;
    }

    /// <summary>
    /// Binds the portal settings from configuration and registers them as singletons.
    /// </summary>
    /// <param name="services">The service collection to add settings to.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The bound application settings.</returns>
    public static AppSettings AddPortalSettings(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);
        services.AddSingleton(appSettings);

        var cognitoConfig = new AwsCognitoConfig();
        configuration.GetSection("AwsCognito").Bind(cognitoConfig);
        services.AddSingleton(cognitoConfig);

        var s3Config = new AwsS3Config();
        configuration.GetSection("AwsS3").Bind(s3Config);
        services.AddSingleton(s3Config);

        return appSettings;
    }

    // Used before the settings are known to be complete; the client is only called once they are.
    private static RegionEndpoint GetRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? RegionEndpoint.USEast1 : RegionEndpoint.GetBySystemName(region);
}
