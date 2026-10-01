using blazwiz.Core.Extensions;
using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.S3;
using blazwiz.Core.Sync.Storage;
using blazwiz.Core.Time.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace blazwiz.Core.UnitTests.Extensions;

public class SyncServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizSync_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SyncServiceCollectionExtensions.AddBlazwizSync(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddBlazwizSync(null!));
    }

    [Fact]
    public async Task GivenDefaults_WhenAddBlazwizSync_ThenEngineServicesAndIndexedDbStoresResolve()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync.AddProfile("notes", p => p.UseRemote("notes/**/*.md", includeEmptyFolders: true)));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<SyncPlanner>(sp.GetRequiredService<ISyncPlanner>());
        Assert.IsType<Sha256ContentHasher>(sp.GetRequiredService<IContentHasher>());
        Assert.IsType<NewestWinsConflictResolver>(sp.GetRequiredService<ISyncConflictResolver>());
        Assert.IsType<S3ReachabilityProbe>(sp.GetRequiredService<IS3ReachabilityProbe>());
        Assert.IsType<IndexedDbClient>(sp.GetRequiredService<IIndexedDbClient>());
        Assert.IsType<BrowserNetworkStatus>(sp.GetRequiredService<IBrowserNetworkStatus>());
        Assert.IsType<WebLockSyncLockService>(sp.GetRequiredService<ISyncLockService>());
        Assert.IsType<StorageDurabilityService>(sp.GetRequiredService<IStorageDurabilityService>());
        Assert.IsType<SyncEngine>(sp.GetRequiredService<ISyncEngine>());
        Assert.IsType<SyncService>(sp.GetRequiredService<ISyncService>());
        Assert.IsType<SyncScheduler>(sp.GetRequiredService<ISyncScheduler>());
        Assert.IsType<ConnectivityService>(sp.GetRequiredService<IConnectivityService>());
        Assert.Same(TimeProvider.System, sp.GetRequiredService<TimeProvider>());
        Assert.NotNull(sp.GetRequiredService<ISystemClock>());

        var log = Assert.IsType<IndexedDbSyncEventLog>(sp.GetRequiredService<ISyncEventLog>());
        Assert.Equal(("blazwiz_sync", "events"), (log.Store.Database, log.Store.Name));

        var index = Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.Equal(("blazwiz_sync", "notes_index"), (index.Store.Database, index.Store.Name));

        var untracked = Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.Equal(("blazwiz_files", "notes"), (untracked.Store.Database, untracked.Store.Name));

        Assert.IsType<TrackingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));
        Assert.IsType<S3SyncRemoteStore>(sp.GetRequiredKeyedService<ISyncRemoteStore>("notes"));
        var matcher = sp.GetRequiredKeyedService<ISyncPathMatcher>("notes");
        Assert.True(matcher.IsMatch("notes/Work/"));

        var profile = Assert.Single(sp.GetServices<SyncProfile>());
        Assert.Equal("notes", profile.Name);
        Assert.Same(untracked, profile.FileStore);
        Assert.Same(index, profile.IndexStore);
        Assert.Equal(["notes"], sp.GetRequiredService<ISyncService>().Profiles);

        var options = sp.GetRequiredService<SyncOptions>();
        Assert.Equal(TimeSpan.FromSeconds(2), options.ChangeDebounce);
    }

    [Fact]
    public async Task GivenIndexAndFilesInDifferentDatabases_WhenAddBlazwizSync_ThenEachStoreUsesItsOwn()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync
            .UseIndexedDbEventLog("blazwiz_sync", "log")
            .AddProfile("notes", p => p
                .UseIndexedDbIndexStore("blazwiz_sync", "notes_index")
                .UseIndexedDbFileStore("blazwiz_notes", "files")
                .UseRemote("notes/**/*.md", includeEmptyFolders: true)));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        var index = Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        var files = Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        var log = Assert.IsType<IndexedDbSyncEventLog>(sp.GetRequiredService<ISyncEventLog>());
        Assert.Equal(("blazwiz_sync", "notes_index"), (index.Store.Database, index.Store.Name));
        Assert.Equal(("blazwiz_notes", "files"), (files.Store.Database, files.Store.Name));
        Assert.Equal("log", log.Store.Name);
    }

    [Fact]
    public async Task GivenCustomStores_WhenAddBlazwizSync_ThenCustomTypesUsedAndStillTracked()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync.AddProfile("notes", p => p
            .UseIndexStore<InMemorySyncIndexStore>()
            .UseFileStore<InMemorySyncFileStore>()
            .UseRemote("notes/**/*.md")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<InMemorySyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.IsType<InMemorySyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.IsType<TrackingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));
    }

    [Fact]
    public async Task GivenMixedStoresAcrossProfiles_WhenAddBlazwizSync_ThenEachProfileGetsItsOwn()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync
            .AddProfile("notes", p => p.UseFileStore<InMemorySyncFileStore>().UseRemote("notes/**/*.md"))
            .AddProfile("photos", p => p.UseIndexStore<InMemorySyncIndexStore>().UseRemote("photos/**/*.png")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.IsType<InMemorySyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.IsType<InMemorySyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("photos"));
        Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("photos" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.Equal(["notes", "photos"], sp.GetServices<SyncProfile>().Select(p => p.Name));
    }

    [Fact]
    public async Task GivenAppOverrides_WhenAddBlazwizSyncAfterThem_ThenAppServicesKept()
    {
        // Arrange
        var services = CreateServices();
        var resolver = Mock.Of<ISyncConflictResolver>();
        services.AddSingleton(resolver);
        services.AddBlazwizSync(sync => sync.UseEventLog<InMemoryLog>().AddProfile("notes", p => p.UseRemote("notes/*.md")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Assert
        Assert.Same(resolver, scope.ServiceProvider.GetRequiredService<ISyncConflictResolver>());
        Assert.IsType<InMemoryLog>(scope.ServiceProvider.GetRequiredService<ISyncEventLog>());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddScoped(_ => Mock.Of<IS3SyncTargetProvider>());
        services.AddScoped(_ => Mock.Of<ISyncUserContext>());
        services.AddScoped(_ => Mock.Of<IOfflineModeStore>());
        return services;
    }

    private sealed class InMemoryLog : ISyncEventLog
    {
        public event EventHandler? EventsWritten;

        public Task WriteAsync(IEnumerable<blazwiz.Core.Sync.Models.SyncEvent> events)
        {
            EventsWritten?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task RecordNoChangePassAsync(string owner, string profile, string passId, DateTimeOffset timestamp) => Task.CompletedTask;

        public Task<blazwiz.Core.Sync.Models.SyncEventPage> QueryAsync(blazwiz.Core.Sync.Models.SyncEventQuery query) => Task.FromResult(new blazwiz.Core.Sync.Models.SyncEventPage());

        public Task ClearAsync() => Task.CompletedTask;

        public Task PruneAsync() => Task.CompletedTask;
    }
}
