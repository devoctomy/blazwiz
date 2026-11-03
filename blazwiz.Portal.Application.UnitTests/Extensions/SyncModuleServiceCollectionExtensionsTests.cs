using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Extensions;
using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Aws;
using blazwiz.Portal.Models.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Extensions;

public class SyncModuleServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizSyncModule_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SyncModuleServiceCollectionExtensions.AddBlazwizSyncModule(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddBlazwizSyncModule(null!));
        Assert.Throws<ArgumentNullException>(() => NotesServiceCollectionExtensions.AddNotes(null!));
        Assert.Throws<ArgumentNullException>(() => NotesServiceCollectionExtensions.AddNotesProfile(null!));
    }

    [Fact]
    public async Task GivenSyncModuleAndNotes_WhenAdded_ThenServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IBrowserInteropService>());
        services.AddSingleton(Mock.Of<IWebAuthnService>());
        services.AddBlazwizCoreServices();
        services.AddSingleton(new AwsCognitoConfig());
        services.AddSingleton(new AwsS3Config());
        services.AddPortalApplicationServices();

        // Act
        var result = services.AddBlazwizSyncModule(sync => sync.AddNotesProfile());
        services.AddNotes();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
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
        Assert.Contains(scope.ServiceProvider.GetServices<NavigationItem>(), i => i.Href == "files");
    }
}
