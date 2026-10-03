using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.UnitTests.Extensions;

public class PrfKeyDerivationServiceCollectionExtensionsTests
{
    private const string ApplicationId = "6f0c6b1e-4b5a-4d8e-9a43-2c1d3e5f7a90";

    [Fact]
    public void GivenNullServices_WhenAddBlazwizPrfKeyDerivation_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PrfKeyDerivationServiceCollectionExtensions.AddBlazwizPrfKeyDerivation(null!, _ => { }));
    }

    [Fact]
    public void GivenNullConfigure_WhenAddBlazwizPrfKeyDerivation_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddBlazwizPrfKeyDerivation(null!));
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizPrfKeyDerivation_ThenServicesAreResolvableAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizPrfKeyDerivation(prf => prf.UseApplication("myapp", ApplicationId).AddKey("apple"));

        // Assert
        Assert.Same(services, result);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.IsType<BouncyCastleHkdfKeyDerivationService>(provider.GetRequiredService<IKeyDerivationService>());
        Assert.IsType<InMemoryDerivedKeyStore>(provider.GetRequiredService<IDerivedKeyStore>());
        Assert.IsType<PrfKeyDerivator>(provider.GetRequiredService<IPrfKeyDerivator>());
        Assert.Same(provider.GetRequiredService<IDerivedKeyStore>(), provider.GetRequiredService<IDerivedKeyStore>());
    }

    [Fact]
    public void GivenCalledByTwoFeatures_WhenAddBlazwizPrfKeyDerivation_ThenKeysFromBothAreDerived()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizPrfKeyDerivation(prf => prf.UseApplication("myapp", ApplicationId).AddKey("apple"));
        services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey("orange"));
        using var provider = services.BuildServiceProvider();

        // Act
        provider.GetRequiredService<IPrfKeyDerivator>().DeriveKeys(new byte[32]);

        // Assert
        Assert.Single(services, d => d.ServiceType == typeof(PrfKeyDerivationOptions));
        Assert.Single(services, d => d.ServiceType == typeof(IPrfKeyDerivator));
        var keyStore = provider.GetRequiredService<IDerivedKeyStore>();
        Assert.True(keyStore.TryGetKey("apple", out _));
        Assert.True(keyStore.TryGetKey("orange", out _));
    }

    [Fact]
    public void GivenSameKeyFromTwoFeatures_WhenAddBlazwizPrfKeyDerivation_ThenThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey("apple"));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey("apple")));
    }
}
