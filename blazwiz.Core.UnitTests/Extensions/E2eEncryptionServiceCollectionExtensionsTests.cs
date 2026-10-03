using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace blazwiz.Core.UnitTests.Extensions;

public class E2eEncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizE2eEncryption_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => E2eEncryptionServiceCollectionExtensions.AddBlazwizE2eEncryption(null!));
    }

    [Fact]
    public async Task GivenAppStores_WhenAddBlazwizE2eEncryption_ThenKeyManagementResolves()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IUserKeysStore>());
        services.AddScoped(_ => Mock.Of<IE2eLocalKeyStore>());

        // Act
        var result = services.AddBlazwizE2eEncryption();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        Assert.IsType<E2eKeyManager>(sp.GetRequiredService<IE2eKeyManager>());
        Assert.IsType<E2eKeyRing>(sp.GetRequiredService<IE2eKeyRing>());
        Assert.IsType<EncryptedFileFormat>(sp.GetRequiredService<IEncryptedFileFormat>());
        Assert.IsType<BouncyCastleArgon2idKeyDerivationService>(sp.GetRequiredService<IArgon2idKeyDerivationService>());
        Assert.Same(sp.GetRequiredService<IE2eKeyRing>(), provider.GetRequiredService<IE2eKeyRing>());
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
