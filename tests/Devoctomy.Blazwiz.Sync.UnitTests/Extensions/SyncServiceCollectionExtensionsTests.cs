using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Extensions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Extensions;
using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Opfs;
using Devoctomy.Blazwiz.Sync.S3;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Extensions;

public class SyncServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizSync_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SyncServiceCollectionExtensions.AddBlazwizSync(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddBlazwizSync(null!));
    }

    [Fact]
    public async Task GivenDefaults_WhenAddBlazwizSync_ThenEngineServicesAndIndexedDbStoresResolve()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync.AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("notes/**/*.md", includeEmptyFolders: true)));
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
        Assert.IsType<OpfsClient>(sp.GetRequiredService<IOpfsClient>());
        Assert.IsType<FolderAccessClient>(sp.GetRequiredService<IFolderAccessClient>());
        Assert.IsType<SyncFolderService>(sp.GetRequiredService<ISyncFolderService>());
        Assert.IsType<SyncStorageChannel>(sp.GetRequiredService<ISyncStorageChannel>());
        Assert.IsType<SyncStorageResolver>(sp.GetRequiredService<ISyncStorageResolver>());
        Assert.IsType<SyncStorageMigrationService>(sp.GetRequiredService<ISyncStorageMigrationService>());
        var storageState = Assert.IsType<IndexedDbSyncStorageStateStore>(sp.GetRequiredService<ISyncStorageStateStore>());
        Assert.Equal(("sync", "storage"), (storageState.Store.Database, storageState.Store.Name));
        Assert.Same(TimeProvider.System, sp.GetRequiredService<TimeProvider>());
        Assert.NotNull(sp.GetRequiredService<TimeProvider>());

        var log = Assert.IsType<IndexedDbSyncEventLog>(sp.GetRequiredService<ISyncEventLog>());
        Assert.Equal(("sync", "events"), (log.Store.Database, log.Store.Name));

        var index = Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.Equal(("sync", "notes_index"), (index.Store.Database, index.Store.Name));

        var untracked = Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.Equal(("files", "notes"), (untracked.Store.Database, untracked.Store.Name));

        Assert.IsType<EncryptingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));
        Assert.IsType<S3SyncRemoteStore>(sp.GetRequiredService<ISyncProfileRegistry>().GetSyncProfile("notes").RemoteStore);
        var matcher = sp.GetRequiredKeyedService<ISyncPathMatcher>("notes");
        Assert.True(matcher.IsMatch("notes/Work/"));

        var profile = sp.GetRequiredService<ISyncProfileRegistry>().GetSyncProfile(Assert.Single(sp.GetRequiredService<ISyncProfileRegistry>().Profiles).Name);
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
            .UseIndexedDbEventLog("sync", "log")
            .AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D"))
                .UseIndexedDbIndexStore("sync", "notes_index")
                .UseIndexedDbFileStore("notes", "files")
                .UseRemote("notes/**/*.md", includeEmptyFolders: true)));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        var index = Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        var files = Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        var log = Assert.IsType<IndexedDbSyncEventLog>(sp.GetRequiredService<ISyncEventLog>());
        Assert.Equal(("sync", "notes_index"), (index.Store.Database, index.Store.Name));
        Assert.Equal(("notes", "files"), (files.Store.Database, files.Store.Name));
        Assert.Equal("log", log.Store.Name);
    }

    [Fact]
    public async Task GivenSwitchableStores_WhenAddBlazwizSync_ThenEngineAndAppGoThroughTheSwitchAndEachStorageResolves()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync.AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D"))
            .UseFileStores(s => s.AddIndexedDb("notes", "files").AddOpfs("notes_root").AddLocalFolder())
            .UseRemote("notes/**/*.md")));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Act
        var resolver = sp.GetRequiredService<ISyncStorageResolver>();

        // Assert
        var untracked = Assert.IsType<SwitchableSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.Same(untracked, sp.GetRequiredService<ISyncProfileRegistry>().GetSyncProfile("notes").FileStore);
        Assert.IsType<EncryptingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));

        var indexedDb = Assert.IsType<IndexedDbSyncFileStore>(resolver.GetStore("notes", SyncStorageKind.IndexedDb));
        Assert.Equal(("notes", "files"), (indexedDb.Store.Database, indexedDb.Store.Name));
        var opfs = Assert.IsType<OpfsSyncFileStore>(resolver.GetStore("notes", SyncStorageKind.Opfs));
        Assert.Equal("notes_root", opfs.Root);
        Assert.Equal(("sync", "notes_opfs"), (opfs.MetadataStore.Database, opfs.MetadataStore.Name));
        var folder = Assert.IsType<FolderSyncFileStore>(resolver.GetStore("notes", SyncStorageKind.LocalFolder));
        Assert.Equal(("sync", "notes_folder"), (folder.MetadataStore.Database, folder.MetadataStore.Name));
    }

    [Fact]
    public async Task GivenCustomStores_WhenAddBlazwizSync_ThenCustomTypesUsedAndStillTracked()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync.AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D"))
            .UseIndexStore<InMemorySyncIndexStore>()
            .UseFileStore<InMemorySyncFileStore>()
            .UseRemote("notes/**/*.md")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<InMemorySyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.IsType<InMemorySyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.IsType<EncryptingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));
    }

    [Fact]
    public async Task GivenMixedStoresAcrossProfiles_WhenAddBlazwizSync_ThenEachProfileGetsItsOwn()
    {
        // Arrange
        var services = CreateServices();
        services.AddBlazwizSync(sync => sync
            .AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseFileStore<InMemorySyncFileStore>().UseRemote("notes/**/*.md"))
            .AddProfile("photos", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseIndexStore<InMemorySyncIndexStore>().UseRemote("photos/**/*.png")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<IndexedDbSyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("notes"));
        Assert.IsType<InMemorySyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.IsType<InMemorySyncIndexStore>(sp.GetRequiredKeyedService<ISyncIndexStore>("photos"));
        Assert.IsType<IndexedDbSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("photos" + SyncDefaults.UntrackedFileStoreKeySuffix));
        Assert.Equal(["notes", "photos"], sp.GetRequiredService<ISyncProfileRegistry>().Profiles.Select(p => p.Name));
    }

    [Fact]
    public async Task GivenAppOverrides_WhenAddBlazwizSyncAfterThem_ThenAppServicesKept()
    {
        // Arrange
        var services = CreateServices();
        var resolver = Mock.Of<ISyncConflictResolver>();
        services.AddSingleton(resolver);
        services.AddBlazwizSync(sync => sync.UseEventLog<InMemoryLog>().AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("notes/*.md")));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Assert
        Assert.Same(resolver, scope.ServiceProvider.GetRequiredService<ISyncConflictResolver>());
        Assert.IsType<InMemoryLog>(scope.ServiceProvider.GetRequiredService<ISyncEventLog>());
    }

    [Fact]
    public async Task GivenProfiles_WhenAddBlazwizSync_ThenEveryAppStoreEncrypts()
    {
        // Arrange
        var services = CreateServices();
        services.AddScoped(_ => Mock.Of<IUserKeysStore>());
        services.AddScoped(_ => Mock.Of<IE2eLocalKeyStore>());
        services.AddScoped(_ => Mock.Of<IE2eProfileIndexStore>());
        services.AddScoped(_ => Mock.Of<IE2eLocalProfileIndexStore>());
        services.AddBlazwizE2eEncryption();
        services.AddBlazwizSync(sync => sync
            .AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("notes/**/*.md"))
            .AddProfile("photos", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("photos/**/*")));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Assert
        Assert.IsType<EncryptingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("notes"));
        Assert.IsType<EncryptingSyncFileStore>(sp.GetRequiredKeyedService<ISyncFileStore>("photos"));
        Assert.Empty(sp.GetServices<ISyncGate>());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddBlazwizApp("app", "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8");
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddScoped(_ => Mock.Of<IS3SyncTargetProvider>());
        services.AddScoped(_ => Mock.Of<ISyncUserContext>());
        services.AddScoped(_ => Mock.Of<ISyncEnabledStore>());
        services.AddSingleton<IE2eKeyRings>(new E2eKeyRings(new InMemoryDerivedKeyStore()));
        return services;
    }

    private sealed class InMemoryLog : ISyncEventLog
    {
        public event EventHandler? EventsWritten;

        public Task WriteAsync(IEnumerable<Devoctomy.Blazwiz.Sync.Models.SyncEvent> events)
        {
            EventsWritten?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task RecordNoChangePassAsync(string owner, string profile, string passId, DateTimeOffset timestamp) => Task.CompletedTask;

        public Task<Devoctomy.Blazwiz.Sync.Models.SyncEventPage> QueryAsync(Devoctomy.Blazwiz.Sync.Models.SyncEventQuery query) => Task.FromResult(new Devoctomy.Blazwiz.Sync.Models.SyncEventPage());

        public Task ClearAsync() => Task.CompletedTask;

        public Task PruneAsync() => Task.CompletedTask;
    }
}
