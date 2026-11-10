using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Opfs;
using Devoctomy.Blazwiz.Sync.S3;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Sync.Extensions;

/// <summary>
/// Extension methods for registering the sync engine in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SyncServiceCollectionExtensions
{
    /// <summary>
    /// Adds the sync engine and its profiles, and keeps the user's keys documents and index of protected profiles
    /// (<see cref="IUserKeysStore"/>, <see cref="IE2eProfileIndexStore"/>) in their prefix of the data bucket, or in
    /// local storage when <see cref="IS3SyncTargetProvider.IsConfigured"/> is false. The app must also register
    /// <see cref="IS3SyncTargetProvider"/>, <see cref="ISyncUserContext"/> and <see cref="ISyncEnabledStore"/>, and the
    /// browser services (<c>AddBlazwizBrowser</c>).
    /// </summary>
    /// <remarks>
    /// Each profile's file store is registered as a keyed <see cref="ISyncFileStore"/> under the profile name, already
    /// wrapped so writes and deletes are tracked: <c>[FromKeyedServices("documents")] ISyncFileStore files</c>. A profile
    /// configured with <c>UseFileStores</c> passes each call to the storage the user picked on this device; the files
    /// are moved between storages with <see cref="ISyncStorageMigrationService"/>.
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Configures the log, timings and profiles.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizSync(this IServiceCollection services, Action<SyncBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SyncOptions();
        configure(new SyncBuilder(options));
        services.AddSingleton(options);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOfflineModeService, OfflineModeService>();
        services.TryAddSingleton<ISyncPlanner, SyncPlanner>();
        services.TryAddSingleton<IContentHasher, Sha256ContentHasher>();
        services.TryAddSingleton<ISyncConflictResolver, NewestWinsConflictResolver>();
        services.TryAddSingleton<IS3ReachabilityProbe, S3ReachabilityProbe>();
        services.TryAddSingleton<IAeadCipher, BouncyCastleAesGcmAeadCipher>();
        services.TryAddSingleton<IEncryptedFileFormat, EncryptedFileFormat>();
        services.TryAddScoped<IIndexedDbClient, IndexedDbClient>();
        services.TryAddScoped<IBrowserNetworkStatus, BrowserNetworkStatus>();
        services.TryAddScoped<ISyncLockService, WebLockSyncLockService>();
        services.TryAddScoped<IStorageDurabilityService, StorageDurabilityService>();
        services.TryAddScoped<ISyncEngine, SyncEngine>();
        services.TryAddScoped<ISyncService, SyncService>();
        services.TryAddScoped<ISyncScheduler, SyncScheduler>();
        services.TryAddScoped<IConnectivityService, ConnectivityService>();
        services.TryAddScoped<IOpfsClient, OpfsClient>();
        services.TryAddScoped<IFolderAccessClient, FolderAccessClient>();
        services.TryAddScoped<ISyncFolderService, SyncFolderService>();
        services.TryAddScoped<ISyncStorageChannel, SyncStorageChannel>();
        services.TryAddScoped<ISyncStorageStateStore>(sp => new IndexedDbSyncStorageStateStore(
            sp.GetRequiredService<IIndexedDbClient>(),
            IndexedDbSyncStorageStateStore.DescribeStore(SyncDefaults.IndexDatabase, SyncDefaults.StorageStateStore)));
        services.TryAddScoped<ISyncStorageResolver, SyncStorageResolver>();
        services.TryAddScoped<ISyncStorageMigrationService, SyncStorageMigrationService>();
        services.TryAddScoped<ISyncEventLog>(sp => options.EventLogFactory?.Invoke(sp) ?? new IndexedDbSyncEventLog(
            sp.GetRequiredService<IIndexedDbClient>(),
            sp.GetRequiredService<ISyncUserContext>(),
            sp.GetRequiredService<TimeProvider>(),
            options,
            IndexedDbSyncEventLog.DescribeStore(options.EventLogDatabase, options.EventLogStore)));

        // The user's keys documents and the index of their protected profiles: in their prefix of the data bucket, or,
        // with no data bucket configured (local development), in this browser's local storage.
        services.TryAddScoped<S3UserKeysStore>();
        services.TryAddScoped<LocalStorageUserKeysStore>();
        services.TryAddScoped<IUserKeysStore>(sp => UsesDataBucket(sp) ? sp.GetRequiredService<S3UserKeysStore>() : sp.GetRequiredService<LocalStorageUserKeysStore>());
        services.TryAddScoped<IE2eProfileIndexStore>(sp => UsesDataBucket(sp) ? sp.GetRequiredService<S3UserKeysStore>() : sp.GetRequiredService<LocalStorageUserKeysStore>());

        // Every profile's stores come from the registry, so profiles added while the app runs work the same way.
        services.TryAddScoped<ISyncProfileRegistry>(sp => new SyncProfileRegistry(sp, options));
        foreach (var profile in options.Profiles)
        {
            AddProfile(services, profile.Name);
        }

        return services;
    }

    private static bool UsesDataBucket(IServiceProvider services) =>
        services.GetRequiredService<IS3SyncTargetProvider>().IsConfigured;

    private static void AddProfile(IServiceCollection services, string name)
    {
        // App code gets a configured profile's stores as keyed services, e.g. [FromKeyedServices("notes")].
        var untrackedKey = name + SyncDefaults.UntrackedFileStoreKeySuffix;
        services.AddKeyedScoped<ISyncPathMatcher>(name, (sp, _) => sp.GetRequiredService<ISyncProfileRegistry>().GetMatcher(name));
        services.AddKeyedScoped<ISyncIndexStore>(name, (sp, _) => sp.GetRequiredService<ISyncProfileRegistry>().GetIndexStore(name));
        services.AddKeyedScoped<ISyncFileStore>(untrackedKey, (sp, _) => sp.GetRequiredService<ISyncProfileRegistry>().GetUntrackedFileStore(name));
        services.AddKeyedScoped<ISyncFileStore>(name, (sp, _) => sp.GetRequiredService<ISyncProfileRegistry>().GetFileStore(name));
    }
}
