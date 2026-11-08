using Blazing.Mvvm;
using Devoctomy.Blazwiz.Auth.Cognito.Extensions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Cryptography.Extensions;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Extensions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Extensions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Storage;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Sync.Components;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Pages;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Devoctomy.Blazwiz.UI.Sync.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.UI.Sync.Extensions;

/// <summary>
/// Extension methods for the sync module: files kept in IndexedDB, OPFS or a folder the user picks (chosen per device in
/// Settings), synced with the user's prefix in the data bucket, with optional end to end encryption.
/// </summary>
public static class SyncModuleServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section holding the data bucket's settings (<see cref="AwsS3Config"/>).
    /// </summary>
    public const string ConfigurationSection = "AwsS3";

    /// <summary>
    /// The configuration section holding the Argon2id settings for new keys, if not the build's defaults.
    /// </summary>
    public const string Argon2idConfigurationSection = "Argon2id";

    /// <summary>
    /// Adds sync and end to end encryption, starts and stops sync with the session, and adds the module's UI: the Sync
    /// page, the status pill, banners and overlay, its Settings sections and its sign-in option. Signs in with Cognito
    /// (<c>AddBlazwizCognito</c>), whose credentials reach the data bucket and the user's keys. Call after
    /// <c>AddBlazwizUi</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration.</param>
    /// <param name="configure">Configures sync: the event log and the app's profiles (what is synced, and where it is
    /// kept on the device).</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizSyncModule(this IServiceCollection services, IConfiguration configuration, Action<SyncBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        // The app registers Cognito itself, saying whether the placeholder sign in may stand in (its host environment).
        // An app that has not is given Cognito with no placeholder: nothing here can judge the environment.
        if (!services.Any(d => d.ServiceType == typeof(AwsCognitoConfig)))
        {
            services.AddBlazwizCognito(configuration, allowPlaceholderSignIn: false);
        }

        var s3Config = new AwsS3Config();
        configuration.GetSection(ConfigurationSection).Bind(s3Config);
        services.AddSingleton(s3Config);

        services.AddScoped<IS3SyncTargetProvider, CognitoS3TargetProvider>();
        services.AddScoped<ISyncUserContext, AuthSyncUserContext>();
        services.AddScoped<ISyncEnabledStore, LocalStorageSyncEnabledStore>();
        services.AddScoped<IPersistentStorageService, PersistentStorageService>();
        services.AddSingleton<ISyncEventPresenter, SyncEventPresenter>();
        services.AddSingleton<ISyncStatusPresenter, SyncStatusPresenter>();
        services.AddScoped<ISyncFolderAccessMonitor, SyncFolderAccessMonitor>();
        services.AddScoped<ISyncProfileDisplayNames, SyncProfileDisplayNames>();
        services.AddScoped<IE2eProfilePresenter, E2eProfilePresenter>();
        services.AddSessionLifecycleHandler<SyncSessionHandler>();
        services.AddErrorText<SyncUiText>();

        // End to end encryption: the user's keys in their own Secrets Manager secrets (<user pool sub>/keys, and
        // <user pool sub>/keys/<id> for each protected profile, listed in <user pool sub>/keys/index), and this
        // device's copy of the master key wrapped by a key derived from the passkey PRF.
        services.AddSingleton<ISecretsManagerClientFactory, SecretsManagerClientFactory>();

        // Without Cognito (local development) there is no Secrets Manager: the keys are kept in the browser instead.
        services.AddScoped<SecretsManagerUserKeysStore>();
        services.AddScoped<LocalStorageUserKeysStore>();
        services.AddScoped<IUserKeysStore>(sp => UsesSecretsManager(sp) ? sp.GetRequiredService<SecretsManagerUserKeysStore>() : sp.GetRequiredService<LocalStorageUserKeysStore>());
        services.AddScoped<IE2eProfileIndexStore>(sp => UsesSecretsManager(sp) ? sp.GetRequiredService<SecretsManagerUserKeysStore>() : sp.GetRequiredService<LocalStorageUserKeysStore>());
        services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey(E2eKeyNames.KeyEncryptionKey).AddKey(E2eKeyNames.LabelKey));
        services.AddBlazwizE2eEncryption(argon2 => configuration.GetSection(Argon2idConfigurationSection).Bind(argon2.Parameters));
        services.AddScoped<IE2eLocalKeyStore, LocalStorageE2eLocalKeyStore>();
        services.AddScoped<IE2eLocalProfileIndexStore, LocalStorageE2eLocalProfileIndexStore>();
        services.AddBlazwizCryptography();
        services.AddScoped<IE2eProfileLabelStore, LocalStorageE2eProfileLabelStore>();
        services.AddScoped<IE2eLockChannel, E2eLockChannel>();
        services.AddScoped<IE2eIdleLock, E2eIdleLock>();
        services.AddScoped<ISyncProtectedProfileService, SyncProtectedProfileService>();

        services.AddBlazwizSync(configure);

        services.AddMvvm(options =>
        {
            options.HostingModelType = BlazorHostingModelType.WebAssembly;
            options.RegisterViewModelsFromAssemblyContaining<SyncViewModel>();
        });

        services.AddPageAssembly(typeof(SyncPage).Assembly);
        services.ReplacePage<SyncView>(BlazwizPages.Sync);

        services.AddShellComponent<E2eBanner>(ShellRegion.Banners, order: 10);
        services.AddShellComponent<FolderAccessBanner>(ShellRegion.Banners, order: 20);
        services.AddShellComponent<SyncPill>(ShellRegion.Floating, order: 0);
        services.AddShellComponent<StorageMigrationOverlay>(ShellRegion.Overlays, order: 0);

        services.AddSettingsSection<SyncSettingsSection>(UiServiceCollectionExtensions.SettingsOrder.Offline + 100);
        services.AddSettingsSection<Argon2idPanel>(UiServiceCollectionExtensions.SettingsOrder.Offline + 200);

        services.AddSignInOption<StartSyncOffSignInOption, StartSyncOffOption>();

        services.AddNavigationItem(new NavigationItem
        {
            Text = "Nav.Sync",
            Description = "Nav.Sync.Description",
            TextResource = typeof(SyncUiText),
            Href = "sync",
            Icon = "bi-arrow-repeat",
            Order = 20,
            PageKey = BlazwizPages.Sync,
        });
        return services;
    }

    private static bool UsesSecretsManager(IServiceProvider services) =>
        CognitoAuthenticationService.IsConfigured(services.GetService<AwsCognitoConfig>());
}
