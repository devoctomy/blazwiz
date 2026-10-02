using Amazon;
//#if (IncludeNotes)
using Amazon.CognitoIdentity;
//#endif
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
//#if (IncludeNotes)
using blazwiz.Core.Extensions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
//#endif
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

        // Cognito when a user pool is configured (deployed), otherwise the placeholder login (local development).
        services.AddScoped<IAuthenticationService>(sp =>
            CognitoAuthenticationService.IsConfigured(sp.GetService<AwsCognitoConfig>())
                ? ActivatorUtilities.CreateInstance<CognitoAuthenticationService>(sp)
                : ActivatorUtilities.CreateInstance<LocalAuthenticationService>(sp));
        services.AddSingleton<INavigationMenuService, NavigationMenuService>();

//#if (IncludeNotes)
        // Sync: notes are plain markdown files kept in IndexedDB or OPFS (the user picks, per device, in Settings) and
        // synced with the user's prefix in the data bucket (docs/wip/s3-sync.md, docs/wip/opfs-storage.md). The identity pool's GetId / GetCredentialsForIdentity are public APIs, so no credentials.
        services.AddSingleton<IAmazonCognitoIdentity>(sp => new AmazonCognitoIdentityClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityConfig
            {
                RegionEndpoint = GetRegion(sp.GetRequiredService<AwsCognitoConfig>().Region),
                HttpClientFactory = sp.GetRequiredService<BrowserHttpClientFactory>(),
            }));
        services.AddScoped<ICognitoIdentityClient, CognitoIdentityClient>();
        services.AddScoped<IS3SyncTargetProvider, CognitoS3TargetProvider>();
        services.AddScoped<ISyncUserContext, AuthSyncUserContext>();
        services.AddScoped<IOfflineModeStore, LocalStorageOfflineModeStore>();
        services.AddScoped<IPersistentStorageService, PersistentStorageService>();
        services.AddSingleton<ISyncEventPresenter, SyncEventPresenter>();
        services.AddSingleton<ISyncStatusPresenter, SyncStatusPresenter>();
        services.AddBlazwizSync(sync => sync
            .UseIndexedDbEventLog(database: "blazwiz_sync", store: "events")
            .UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)
            .UseChangeDebounce(TimeSpan.FromSeconds(2))
            .AddProfile(NoteService.ProfileName, profile => profile
                .UseIndexedDbIndexStore(database: "blazwiz_sync", store: "notes_index")
                .UseFileStores(stores => stores
                    .AddIndexedDb(database: IndexedDbNotesStateStore.Database, store: "files")
                    .AddOpfs(root: "blazwiz_notes")
                    .UseDefault(SyncStorageKind.IndexedDb))
                .UseRemote("notes/**/*.md", includeEmptyFolders: true)));

        // Notes
        services.AddScoped<INotesStateStore, IndexedDbNotesStateStore>();
        services.AddScoped<INoteMigrationService, NoteMigrationService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<INoteNameService, NoteNameService>();
        services.AddSingleton<INoteTreeService, NoteTreeService>();
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddSingleton<IMarkdownFormatService, MarkdownFormatService>();

//#endif
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
//#if (IncludeNotes)

    // Used before the settings are known to be complete; the client is only called once they are.
    private static RegionEndpoint GetRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? RegionEndpoint.USEast1 : RegionEndpoint.GetBySystemName(region);
//#endif
}
