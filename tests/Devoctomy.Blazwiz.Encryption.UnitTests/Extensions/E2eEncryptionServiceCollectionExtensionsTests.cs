using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Extensions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Devoctomy.Blazwiz.Encryption.UnitTests.Extensions;

public class E2eEncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizE2eEncryption_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => E2eEncryptionServiceCollectionExtensions.AddBlazwizE2eEncryption(null!));
    }

    [Fact]
    public async Task GivenAppStores_WhenAddBlazwizE2eEncryption_ThenKeyManagementResolves()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IUserKeysStore>());
        services.AddScoped(_ => Mock.Of<IE2eLocalKeyStore>());
        services.AddScoped(_ => Mock.Of<IE2eProfileIndexStore>());
        services.AddScoped(_ => Mock.Of<IE2eLocalProfileIndexStore>());

        // Act
        var result = services.AddBlazwizE2eEncryption();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        Assert.IsType<E2eProtectedProfileManager>(sp.GetRequiredService<IE2eProtectedProfileManager>());
        Assert.IsType<E2eKeysDocumentService>(sp.GetRequiredService<IE2eKeysDocumentService>());
        Assert.IsType<EncryptedFileFormat>(sp.GetRequiredService<IEncryptedFileFormat>());
        Assert.Same(sp.GetRequiredService<IE2eKeyRings>(), provider.GetRequiredService<IE2eKeyRings>());
        Assert.IsType<BouncyCastleArgon2idKeyDerivationService>(sp.GetRequiredService<IArgon2idKeyDerivationService>());
    }

    [Fact]
    public void GivenConfigure_WhenAddBlazwizE2eEncryption_ThenArgon2idOptionsReplaceCoreDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizCoreServices();

        // Act
        services.AddBlazwizE2eEncryption(o => o.Parameters.MemoryKib = 1024);

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.Equal(1024, provider.GetRequiredService<Argon2idOptions>().Parameters.MemoryKib);
        Assert.Single(services, d => d.ServiceType == typeof(Argon2idOptions));
    }
}
