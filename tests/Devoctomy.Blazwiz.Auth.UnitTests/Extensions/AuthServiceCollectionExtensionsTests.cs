using Devoctomy.Blazwiz.TestSupport;
using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Extensions;

public class AuthServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAdding_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AuthServiceCollectionExtensions.AddBlazwizAuth(null!));
        Assert.Throws<ArgumentNullException>(() => AuthServiceCollectionExtensions.AddSessionLifecycleHandler<TestHandler>(null!));
    }

    [Fact]
    public void GivenAppNotAdded_WhenAddBlazwizAuth_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddBlazwizAuth());
    }

    [Fact]
    public async Task GivenApp_WhenAddBlazwizAuth_ThenSignInServicesAreResolvable()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        var result = services.AddBlazwizAuth();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<PasswordCryptoService>(scope.ServiceProvider.GetRequiredService<IPasswordCryptoService>());
        Assert.IsType<UserDisplayService>(scope.ServiceProvider.GetRequiredService<IUserDisplayService>());
        Assert.IsType<InMemoryOfflineSessionStore>(scope.ServiceProvider.GetRequiredService<IOfflineSessionStore>());
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<LocalPasswordService>(scope.ServiceProvider.GetRequiredService<IPasswordService>());
        Assert.IsType<SessionLifecycle>(scope.ServiceProvider.GetRequiredService<ISessionLifecycle>());
        Assert.IsType<PrfKeyDerivator>(scope.ServiceProvider.GetRequiredService<IPrfKeyDerivator>());
        Assert.IsType<InMemoryDerivedKeyStore>(scope.ServiceProvider.GetRequiredService<IDerivedKeyStore>());
        Assert.Equal("testapp_session", scope.ServiceProvider.GetRequiredService<IAppStorageKeys>().For("session"));
    }

    [Fact]
    public void GivenApp_WhenAddBlazwizAuth_ThenPasskeyKeysAreDerivedForTheApp()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizAuth();

        // Assert
        using var provider = services.BuildServiceProvider();
        var prfOptions = provider.GetRequiredService<PrfKeyDerivationOptions>();
        Assert.Equal(TestServices.AppName, prfOptions.ApplicationName);
        Assert.Equal(Guid.Parse(TestServices.AppId), prfOptions.ApplicationId);
        Assert.Contains(PasswordCryptoService.RememberMeKeyName, prfOptions.Keys.Select(k => k.Name));
    }

    [Fact]
    public void GivenAuthAdded_WhenAddBlazwizAuthAgain_ThenNothingIsAddedTwice()
    {
        // Arrange
        var services = TestServices.Create();
        services.AddBlazwizAuth();
        var count = services.Count;

        // Act
        services.AddBlazwizAuth();

        // Assert
        Assert.Equal(count, services.Count);
    }

    [Fact]
    public async Task GivenHandlerAddedTwice_WhenAddSessionLifecycleHandler_ThenRegisteredOnce()
    {
        // Arrange
        var services = TestServices.Create();
        services.AddBlazwizAuth();

        // Act
        services.AddSessionLifecycleHandler<TestHandler>();
        services.AddSessionLifecycleHandler<TestHandler>();

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<TestHandler>(Assert.Single(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>()));
    }

    private sealed class TestHandler : ISessionLifecycleHandler
    {
    }
}
