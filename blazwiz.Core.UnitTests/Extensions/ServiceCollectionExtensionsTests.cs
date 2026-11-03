using blazwiz.Core.Configuration;
using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizCoreServices_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddBlazwizCoreServices(null!));
    }

    private const string AppId = "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8";

    [Fact]
    public void GivenNameAndId_WhenAddBlazwizApp_ThenOptionsAndStorageKeysResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizApp("acmeportal", AppId);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Same(services, result);
        var options = provider.GetRequiredService<BlazwizAppOptions>();
        Assert.Equal("acmeportal", options.Name);
        Assert.Equal(AppId, options.Id);
        Assert.Equal("acmeportal_session", provider.GetRequiredService<IAppStorageKeys>().For("session"));
    }

    [Theory]
    [InlineData("AcmePortal")]
    [InlineData("1app")]
    [InlineData("acme-portal")]
    [InlineData("acme_portal")]
    public void GivenInvalidName_WhenAddBlazwizApp_ThenThrowsArgumentException(string name)
    {
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBlazwizApp(name, AppId)).ParamName);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GivenInvalidId_WhenAddBlazwizApp_ThenThrowsArgumentException(string id)
    {
        Assert.Equal("id", Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBlazwizApp("acmeportal", id)).ParamName);
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenReturnsSameCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizCoreServices();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenCoreServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizCoreServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var cryptoService = scope.ServiceProvider.GetRequiredService<ICryptoService>();

        // Assert
        Assert.Same(TimeProvider.System, timeProvider);
        Assert.IsType<BouncyCastleAesGcmCryptoService>(cryptoService);
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenKeyCryptographyIsResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizCoreServices();
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Assert.IsType<BouncyCastleAesGcmAeadCipher>(provider.GetRequiredService<IAeadCipher>());
        Assert.IsType<AeadKeyWrapService>(provider.GetRequiredService<IKeyWrapService>());
        Assert.IsType<BouncyCastleArgon2idKeyDerivationService>(provider.GetRequiredService<IArgon2idKeyDerivationService>());
        Assert.NotNull(provider.GetRequiredService<Argon2idOptions>());
    }
}
