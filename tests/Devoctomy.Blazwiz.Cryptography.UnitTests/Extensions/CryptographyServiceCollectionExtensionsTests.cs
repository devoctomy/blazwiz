using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Extensions;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Cryptography.UnitTests.Extensions;

public class CryptographyServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizCryptography_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CryptographyServiceCollectionExtensions.AddBlazwizCryptography(null!));
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCryptography_ThenCryptographyIsResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizCryptography().AddBlazwizCryptography();

        // Assert
        Assert.Same(services, result);
        Assert.Single(services, d => d.ServiceType == typeof(IAeadCipher));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.IsType<BouncyCastleAesGcmAeadCipher>(provider.GetRequiredService<IAeadCipher>());
        Assert.IsType<AeadKeyWrapService>(provider.GetRequiredService<IKeyWrapService>());
        Assert.IsType<BouncyCastleArgon2idKeyDerivationService>(provider.GetRequiredService<IArgon2idKeyDerivationService>());
        Assert.NotNull(provider.GetRequiredService<Argon2idOptions>());
    }
}
