using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;
using blazwiz.Portal.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Application.Extensions;

/// <summary>
/// Extension methods for registering portal application services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all portal application services to the dependency injection container.
    /// Browser specific services (<see cref="INavigationService"/>, <see cref="IBrowserInteropService"/>, <see cref="IWebAuthnService"/>) are registered by the portal itself.
    /// Call <see cref="AddPortalSettings"/> first so the Cognito settings decide which authentication service is used.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddPortalApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILocalStorageService, LocalStorageService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IPasswordCryptoService, PasswordCryptoService>();
        services.AddScoped<ICognitoUserPoolClient>(sp =>
        {
            var cognitoConfig = sp.GetRequiredService<AwsCognitoConfig>();
            return new CognitoUserPoolClient(CognitoUserPoolClient.CreateProvider(cognitoConfig), cognitoConfig);
        });

        // Cognito when a user pool is configured (deployed), otherwise the placeholder login (local development).
        services.AddScoped<IAuthenticationService>(sp =>
            CognitoAuthenticationService.IsConfigured(sp.GetService<AwsCognitoConfig>())
                ? ActivatorUtilities.CreateInstance<CognitoAuthenticationService>(sp)
                : ActivatorUtilities.CreateInstance<LocalAuthenticationService>(sp));
        services.AddSingleton<INavigationMenuService, NavigationMenuService>();

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
}
