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
    public void GivenServices_WhenAddPortalApplicationServices_ThenServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IBrowserInteropService>());
        services.AddBlazwizCoreServices();

        // Act
        var result = services.AddPortalApplicationServices();

        // Assert
        Assert.Same(services, result);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.IsType<LocalStorageService>(scope.ServiceProvider.GetRequiredService<ILocalStorageService>());
        Assert.IsType<UserSettingsService>(scope.ServiceProvider.GetRequiredService<IUserSettingsService>());
        Assert.IsType<ThemeService>(scope.ServiceProvider.GetRequiredService<IThemeService>());
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<NavigationMenuService>(scope.ServiceProvider.GetRequiredService<INavigationMenuService>());
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
