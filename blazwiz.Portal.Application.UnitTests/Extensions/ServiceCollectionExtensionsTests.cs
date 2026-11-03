using Amazon.CognitoIdentityProvider;
//#if (IncludeNotes)
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
//#endif
using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Extensions;
using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;
using blazwiz.Portal.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddPortalApplicationServices_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Application.Extensions.ServiceCollectionExtensions.AddPortalApplicationServices(null!));
    }

    [Fact]
    public async Task GivenServices_WhenAddPortalApplicationServices_ThenServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IBrowserInteropService>());
        services.AddSingleton(Mock.Of<IWebAuthnService>());
        services.AddBlazwizCoreServices();
        services.AddSingleton(new AwsCognitoConfig()); // Registered by AddPortalSettings in the portal.
        services.AddSingleton(new AwsS3Config());

        // Act
        var result = services.AddPortalApplicationServices();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<LocalStorageService>(scope.ServiceProvider.GetRequiredService<ILocalStorageService>());
        Assert.IsType<UserSettingsService>(scope.ServiceProvider.GetRequiredService<IUserSettingsService>());
        Assert.IsType<ThemeService>(scope.ServiceProvider.GetRequiredService<IThemeService>());
        Assert.IsType<PasswordCryptoService>(scope.ServiceProvider.GetRequiredService<IPasswordCryptoService>());
        Assert.IsType<PrfKeyDerivator>(scope.ServiceProvider.GetRequiredService<IPrfKeyDerivator>());
        Assert.IsType<InMemoryDerivedKeyStore>(scope.ServiceProvider.GetRequiredService<IDerivedKeyStore>());
        var prfOptions = scope.ServiceProvider.GetRequiredService<PrfKeyDerivationOptions>();
        Assert.Equal(AppIdentity.Name, prfOptions.ApplicationName);
        Assert.Equal(Guid.Parse(AppIdentity.Id), prfOptions.ApplicationId);
        Assert.Contains(PasswordCryptoService.RememberMeKeyName, prfOptions.Keys.Select(k => k.Name));
//#if (IncludeNotes)
        Assert.Contains(E2eKeyNames.KeyEncryptionKey, prfOptions.Keys.Select(k => k.Name));
//#endif
        Assert.IsType<UserDisplayService>(scope.ServiceProvider.GetRequiredService<IUserDisplayService>());
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<NavigationMenuService>(scope.ServiceProvider.GetRequiredService<INavigationMenuService>());
        Assert.IsType<CognitoIdentityClient>(scope.ServiceProvider.GetRequiredService<ICognitoIdentityClient>());
        Assert.IsType<CognitoAwsCredentialsProvider>(scope.ServiceProvider.GetRequiredService<IAwsCredentialsProvider>());
        Assert.IsType<SecretsManagerClientFactory>(scope.ServiceProvider.GetRequiredService<ISecretsManagerClientFactory>());
        Assert.IsType<SecretsManagerUserKeysStore>(scope.ServiceProvider.GetRequiredService<IUserKeysStore>());
//#if (IncludeNotes)
        Assert.IsType<CognitoS3TargetProvider>(scope.ServiceProvider.GetRequiredService<IS3SyncTargetProvider>());
        Assert.IsType<AuthSyncUserContext>(scope.ServiceProvider.GetRequiredService<ISyncUserContext>());
        Assert.IsType<LocalStorageSyncEnabledStore>(scope.ServiceProvider.GetRequiredService<ISyncEnabledStore>());
        Assert.IsType<InMemoryOfflineSessionStore>(scope.ServiceProvider.GetRequiredService<IOfflineSessionStore>());
        Assert.IsType<PersistentStorageService>(scope.ServiceProvider.GetRequiredService<IPersistentStorageService>());
        Assert.IsType<SyncEventPresenter>(scope.ServiceProvider.GetRequiredService<ISyncEventPresenter>());
        Assert.IsType<SyncStatusPresenter>(scope.ServiceProvider.GetRequiredService<ISyncStatusPresenter>());
        Assert.IsType<EncryptingSyncFileStore>(scope.ServiceProvider.GetRequiredKeyedService<ISyncFileStore>(NoteService.ProfileName));
        Assert.IsType<E2eKeyManager>(scope.ServiceProvider.GetRequiredService<IE2eKeyManager>());
        Assert.IsType<LocalStorageE2eLocalKeyStore>(scope.ServiceProvider.GetRequiredService<IE2eLocalKeyStore>());
        Assert.IsType<PasskeyUnlockService>(scope.ServiceProvider.GetRequiredService<IPasskeyUnlockService>());
        Assert.IsType<SyncEncryptionService>(scope.ServiceProvider.GetRequiredService<ISyncEncryptionService>());
        Assert.IsType<E2eSyncGate>(Assert.Single(scope.ServiceProvider.GetServices<ISyncGate>()));
        Assert.Equal([NoteService.ProfileName], scope.ServiceProvider.GetRequiredService<ISyncService>().Profiles);
        Assert.IsType<IndexedDbNotesStateStore>(scope.ServiceProvider.GetRequiredService<INotesStateStore>());
        Assert.IsType<NoteService>(scope.ServiceProvider.GetRequiredService<INoteService>());
        Assert.IsType<NoteNameService>(scope.ServiceProvider.GetRequiredService<INoteNameService>());
        Assert.IsType<NoteTreeService>(scope.ServiceProvider.GetRequiredService<INoteTreeService>());
        Assert.IsType<MarkdownRenderer>(scope.ServiceProvider.GetRequiredService<IMarkdownRenderer>());
        Assert.IsType<MarkdownFormatService>(scope.ServiceProvider.GetRequiredService<IMarkdownFormatService>());
//#endif
    }

    [Fact]
    public void GivenCognitoConfigured_WhenAddPortalApplicationServices_ThenUsesCognitoAuthentication()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IBrowserInteropService>());
        services.AddSingleton(Mock.Of<IWebAuthnService>());
        services.AddBlazwizCoreServices();
        services.AddSingleton(new AwsCognitoConfig { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client" });
        services.AddSingleton(new AwsS3Config());

        // Act
        services.AddPortalApplicationServices();

        // Assert
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.IsType<CognitoAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<CognitoUserPoolClient>(scope.ServiceProvider.GetRequiredService<ICognitoUserPoolClient>());
    }

    [Fact]
    public void GivenCognitoConfigured_WhenAddPortalApplicationServices_ThenCognitoApiClientUsesRegionAndBrowserHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new AwsCognitoConfig { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client" });
        services.AddSingleton(new AwsS3Config());

        // Act
        services.AddPortalApplicationServices();

        // Assert
        using var provider = services.BuildServiceProvider();
        var client = Assert.IsType<AmazonCognitoIdentityProviderClient>(provider.GetRequiredService<IAmazonCognitoIdentityProvider>());
        Assert.Equal("eu-west-2", client.Config.RegionEndpoint.SystemName);
        Assert.Same(provider.GetRequiredService<BrowserHttpClientFactory>(), client.Config.HttpClientFactory);
        Assert.Same(client, provider.GetRequiredService<IAmazonCognitoIdentityProvider>());
    }

    [Fact]
    public void GivenEmptyCognitoConfig_WhenAddPortalApplicationServices_ThenUsesPlaceholderAuthentication()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddBlazwizCoreServices();
        services.AddSingleton(new AwsCognitoConfig());

        // Act
        services.AddPortalApplicationServices();

        // Assert
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
    }

    [Fact]
    public void GivenNullServices_WhenAddPortalSettings_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Application.Extensions.ServiceCollectionExtensions.AddPortalSettings(null!, new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void GivenNullConfiguration_WhenAddPortalSettings_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddPortalSettings(null!));
    }

    [Fact]
    public void GivenConfiguration_WhenAddPortalSettings_ThenBindsAndRegistersSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:ApplicationTitle"] = "My App",
                ["AppSettings:ApplicationDescription"] = "Description",
                ["AppSettings:CopyrightMessage"] = "(c) Me",
                ["AwsCognito:Region"] = "eu-west-2",
                ["AwsCognito:UserPoolId"] = "pool",
                ["AwsCognito:UserPoolClientId"] = "client",
                ["AwsCognito:IdentityPoolId"] = "identity",
                ["AwsS3:Region"] = "eu-west-1",
                ["AwsS3:DataBucketName"] = "data-bucket",
            })
            .Build();
        var services = new ServiceCollection();

        // Act
        var appSettings = services.AddPortalSettings(configuration);

        // Assert
        Assert.Equal("My App", appSettings.ApplicationTitle);
        Assert.Equal("Description", appSettings.ApplicationDescription);
        Assert.Equal("(c) Me", appSettings.CopyrightMessage);

        using var provider = services.BuildServiceProvider();
        Assert.Same(appSettings, provider.GetRequiredService<AppSettings>());
        var cognito = provider.GetRequiredService<AwsCognitoConfig>();
        Assert.Equal("eu-west-2", cognito.Region);
        Assert.Equal("pool", cognito.UserPoolId);
        Assert.Equal("client", cognito.UserPoolClientId);
        Assert.Equal("identity", cognito.IdentityPoolId);
        var s3 = provider.GetRequiredService<AwsS3Config>();
        Assert.Equal("eu-west-1", s3.Region);
        Assert.Equal("data-bucket", s3.DataBucketName);
    }

    [Fact]
    public void GivenEmptyConfiguration_WhenAddPortalSettings_ThenDefaultsAreUsed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var appSettings = services.AddPortalSettings(new ConfigurationBuilder().Build());

        // Assert
        Assert.Equal("blazwiz", appSettings.ApplicationTitle);
    }
}
