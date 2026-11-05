using Devoctomy.Blazwiz.TestSupport;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Extensions;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Devoctomy.Blazwiz.UI.Sync.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Extensions;

public class SyncModuleServiceCollectionExtensionsTests
{
    private const string Profile = "files";

    [Fact]
    public void GivenNullArguments_WhenAddBlazwizSyncModule_ThenThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SyncModuleServiceCollectionExtensions.AddBlazwizSyncModule(null!, configuration, _ => { }));
        Assert.Throws<ArgumentNullException>(() => TestServices.Create().AddBlazwizSyncModule(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => TestServices.Create().AddBlazwizSyncModule(configuration, null!));
    }

    [Fact]
    public async Task GivenCognitoConfigured_WhenAddBlazwizSyncModule_ThenTheKeysAreKeptInSecretsManager()
    {
        // Arrange
        var services = TestServices.Create();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AwsCognito:Region"] = "eu-west-2",
            ["AwsCognito:UserPoolId"] = "eu-west-2_pool",
            ["AwsCognito:UserPoolClientId"] = "client",
        }).Build();
        services.AddBlazwizUi(configuration);

        // Act
        services.AddBlazwizSyncModule(configuration, sync => sync.AddProfile(Profile, profile => profile.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("files/**/*")));

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<SecretsManagerUserKeysStore>(scope.ServiceProvider.GetRequiredService<IUserKeysStore>());
        Assert.IsType<SecretsManagerUserKeysStore>(scope.ServiceProvider.GetRequiredService<IE2eProfileIndexStore>());
    }

    [Fact]
    public async Task GivenUi_WhenAddBlazwizSyncModule_ThenServicesAndViewModelsAreResolvable()
    {
        // Arrange
        var services = TestServices.Create();
        var configuration = new ConfigurationBuilder().Build();
        services.AddBlazwizUi(configuration);

        // Act
        var result = services.AddBlazwizSyncModule(configuration, sync => sync.AddProfile(Profile, profile => profile.UseKeyId(Guid.NewGuid().ToString("D"))
            .UseIndexedDbIndexStore(database: "sync", store: "files_index")
            .UseFileStores(stores => stores
                .AddIndexedDb(database: "files", store: "files")
                .AddOpfs(root: "files")
                .UseDefault(SyncStorageKind.IndexedDb))
            .UseRemote("files/**/*")
            ));

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        var prfOptions = scope.ServiceProvider.GetRequiredService<PrfKeyDerivationOptions>();
        Assert.Contains(E2eKeyNames.KeyEncryptionKey, prfOptions.Keys.Select(k => k.Name));
        Assert.IsType<CognitoS3TargetProvider>(scope.ServiceProvider.GetRequiredService<IS3SyncTargetProvider>());
        Assert.IsType<AuthSyncUserContext>(scope.ServiceProvider.GetRequiredService<ISyncUserContext>());
        Assert.IsType<LocalStorageSyncEnabledStore>(scope.ServiceProvider.GetRequiredService<ISyncEnabledStore>());
        Assert.IsType<PersistentStorageService>(scope.ServiceProvider.GetRequiredService<IPersistentStorageService>());
        Assert.IsType<SyncEventPresenter>(scope.ServiceProvider.GetRequiredService<ISyncEventPresenter>());
        Assert.IsType<SyncStatusPresenter>(scope.ServiceProvider.GetRequiredService<ISyncStatusPresenter>());
        Assert.IsType<SyncFolderAccessMonitor>(scope.ServiceProvider.GetRequiredService<ISyncFolderAccessMonitor>());
        Assert.Contains(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>(), h => h is SyncSessionHandler);
        Assert.IsType<SecretsManagerClientFactory>(scope.ServiceProvider.GetRequiredService<ISecretsManagerClientFactory>());
        // No Cognito configured: the keys are kept in the browser.
        Assert.IsType<LocalStorageUserKeysStore>(scope.ServiceProvider.GetRequiredService<IUserKeysStore>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IUserKeysStore>(), scope.ServiceProvider.GetRequiredService<IE2eProfileIndexStore>());
        Assert.IsType<LocalStorageE2eLocalProfileIndexStore>(scope.ServiceProvider.GetRequiredService<IE2eLocalProfileIndexStore>());
        Assert.IsType<E2eProtectedProfileManager>(scope.ServiceProvider.GetRequiredService<IE2eProtectedProfileManager>());
        Assert.IsType<EncryptingSyncFileStore>(scope.ServiceProvider.GetRequiredKeyedService<ISyncFileStore>(Profile));
        Assert.IsType<LocalStorageE2eLocalKeyStore>(scope.ServiceProvider.GetRequiredService<IE2eLocalKeyStore>());
        Assert.IsType<E2eProfilePresenter>(scope.ServiceProvider.GetRequiredService<IE2eProfilePresenter>());
        Assert.IsType<SyncProfileDisplayNames>(scope.ServiceProvider.GetRequiredService<ISyncProfileDisplayNames>());
        Assert.Empty(scope.ServiceProvider.GetServices<ISyncGate>());
        Assert.Equal([Profile], scope.ServiceProvider.GetRequiredService<ISyncService>().Profiles);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SyncViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SyncSettingsViewModel>());
        Assert.Equal(typeof(SyncView), scope.ServiceProvider.GetRequiredService<IPageRegistry>().GetComponent(BlazwizPages.Sync));
        Assert.Contains(scope.ServiceProvider.GetServices<PageAssemblyRegistration>(), p => p.Assembly == typeof(SyncView).Assembly);
        Assert.Contains(scope.ServiceProvider.GetServices<NavigationItem>(), i => i.PageKey == BlazwizPages.Sync);
    }

    [Fact]
    public void GivenConfiguration_WhenAddBlazwizSyncModule_ThenDataBucketSettingsAreBound()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AwsS3:Region"] = "eu-west-1",
                ["AwsS3:DataBucketName"] = "data-bucket",
            })
            .Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizSyncModule(configuration, sync => { });

        // Assert
        using var provider = services.BuildServiceProvider();
        var s3 = provider.GetRequiredService<AwsS3Config>();
        Assert.Equal("eu-west-1", s3.Region);
        Assert.Equal("data-bucket", s3.DataBucketName);
    }

    [Fact]
    public void GivenArgon2idConfigured_WhenAddBlazwizSyncModule_ThenNewKeysUseTheConfiguredSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Argon2id:MemoryKib"] = "19456",
            })
            .Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizSyncModule(configuration, sync => { });

        // Assert
        using var provider = services.BuildServiceProvider();
        var parameters = provider.GetRequiredService<Argon2idOptions>().Parameters;
        Assert.Equal(19456, parameters.MemoryKib);
        Assert.Equal(2, parameters.Iterations);
    }

    [Fact]
    public void GivenNoArgon2idConfigured_WhenAddBlazwizSyncModule_ThenNewKeysUseTheBuildDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizSyncModule(configuration, sync => { });

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.Equal(new Argon2idOptions().Parameters.MemoryKib, provider.GetRequiredService<Argon2idOptions>().Parameters.MemoryKib);
    }
}
