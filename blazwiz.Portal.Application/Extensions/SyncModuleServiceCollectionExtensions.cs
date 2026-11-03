using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Extensions;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Text;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Application.Extensions;

/// <summary>
/// Extension methods for the sync module: files kept in IndexedDB, OPFS or a folder the user picks (chosen per device in
/// Settings), synced with the user's prefix in the data bucket, with optional end to end encryption.
/// </summary>
public static class SyncModuleServiceCollectionExtensions
{
    /// <summary>
    /// Adds sync and end to end encryption, and starts and stops sync with the session.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures sync: the event log and the app's profiles (what is synced, and where it is
    /// kept on the device).</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizSyncModule(this IServiceCollection services, Action<SyncBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddScoped<IS3SyncTargetProvider, CognitoS3TargetProvider>();
        services.AddScoped<ISyncUserContext, AuthSyncUserContext>();
        services.AddScoped<ISyncEnabledStore, LocalStorageSyncEnabledStore>();
        services.AddScoped<IPersistentStorageService, PersistentStorageService>();
        services.AddSingleton<ISyncEventPresenter, SyncEventPresenter>();
        services.AddSingleton<ISyncStatusPresenter, SyncStatusPresenter>();
        services.AddScoped<ISyncFolderAccessMonitor, SyncFolderAccessMonitor>();
        services.AddSessionLifecycleHandler<SyncSessionHandler>();
        services.AddErrorText<SyncUiText>();

        // End to end encryption: keys.json in Secrets Manager, this device's copy of the master key wrapped by a key
        // derived from the passkey PRF.
        services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey(E2eKeyNames.KeyEncryptionKey));
        services.AddBlazwizE2eEncryption();
        services.AddScoped<IE2eLocalKeyStore, LocalStorageE2eLocalKeyStore>();
        services.AddScoped<IPasskeyUnlockService, PasskeyUnlockService>();

        services.AddBlazwizSync(configure);
        return services;
    }
}
