using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Folder;
using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Opfs;
using blazwiz.Core.Sync.S3;
using blazwiz.Core.Sync.Storage;
using blazwiz.Core.Time;
using blazwiz.Core.Time.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace blazwiz.Core.Extensions;

/// <summary>
/// Extension methods for registering the sync engine in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SyncServiceCollectionExtensions
{
    /// <summary>
    /// Adds the sync engine and its profiles. The app must also register <see cref="IS3SyncTargetProvider"/>,
    /// <see cref="ISyncUserContext"/> and <see cref="IOfflineModeStore"/>.
    /// </summary>
    /// <remarks>
    /// Each profile's file store is registered as a keyed <see cref="ISyncFileStore"/> under the profile name, already
    /// wrapped so writes and deletes are tracked: <c>[FromKeyedServices("notes")] ISyncFileStore files</c>. A profile
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
        services.TryAddSingleton<ISystemClock, SystemClock>();
        services.TryAddSingleton<ISyncPlanner, SyncPlanner>();
        services.TryAddSingleton<IContentHasher, Sha256ContentHasher>();
        services.TryAddSingleton<ISyncConflictResolver, NewestWinsConflictResolver>();
        services.TryAddSingleton<IS3ReachabilityProbe, S3ReachabilityProbe>();
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
            sp.GetRequiredService<ISystemClock>(),
            options,
            IndexedDbSyncEventLog.DescribeStore(options.EventLogDatabase, options.EventLogStore)));

        foreach (var profile in options.Profiles)
        {
            AddProfile(services, profile);
        }

        return services;
    }

    private static void AddProfile(IServiceCollection services, SyncProfileOptions profile)
    {
        var name = profile.Name;
        var untrackedKey = name + SyncDefaults.UntrackedFileStoreKeySuffix;

        services.AddKeyedSingleton<ISyncPathMatcher>(name, (_, _) => new GlobSyncPathMatcher(profile.RemotePattern, profile.IncludeEmptyFolders));

        services.AddKeyedScoped<ISyncIndexStore>(name, (sp, _) => profile.IndexStoreFactory?.Invoke(sp) ?? new IndexedDbSyncIndexStore(
            sp.GetRequiredService<IIndexedDbClient>(),
            IndexedDbSyncIndexStore.DescribeStore(profile.IndexDatabase, profile.ResolvedIndexStoreName)));

        services.AddKeyedScoped<ISyncFileStore>(untrackedKey, (sp, _) => profile.IsSwitchable
            ? new SwitchableSyncFileStore(name, sp.GetRequiredService<ISyncStorageResolver>())
            : profile.FileStoreFactory?.Invoke(sp) ?? new IndexedDbSyncFileStore(
                sp.GetRequiredService<IIndexedDbClient>(),
                sp.GetRequiredService<ISyncUserContext>(),
                sp.GetRequiredService<ISystemClock>(),
                IndexedDbSyncFileStore.DescribeStore(profile.FileDatabase, profile.ResolvedFileStoreName)));

        services.AddKeyedScoped<ISyncFileStore>(name, (sp, _) => new TrackingSyncFileStore(
            name,
            sp.GetRequiredKeyedService<ISyncFileStore>(untrackedKey),
            sp.GetRequiredKeyedService<ISyncIndexStore>(name),
            sp.GetRequiredService<ISyncUserContext>(),
            sp.GetRequiredService<ISyncScheduler>(),
            sp.GetRequiredService<ISystemClock>()));

        services.AddKeyedScoped<ISyncRemoteStore>(name, (sp, _) => new S3SyncRemoteStore(
            sp.GetRequiredService<IS3SyncTargetProvider>(),
            sp.GetRequiredKeyedService<ISyncPathMatcher>(name)));

        services.AddScoped(sp => new SyncProfile(
            name,
            sp.GetRequiredKeyedService<ISyncFileStore>(untrackedKey),
            sp.GetRequiredKeyedService<ISyncIndexStore>(name),
            sp.GetRequiredKeyedService<ISyncRemoteStore>(name),
            sp.GetRequiredKeyedService<ISyncPathMatcher>(name)));
    }
}
