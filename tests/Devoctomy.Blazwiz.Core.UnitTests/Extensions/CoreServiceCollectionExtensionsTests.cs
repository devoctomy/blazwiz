using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Core.UnitTests.Extensions;

public class CoreServiceCollectionExtensionsTests
{
    private const string AppId = "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8";

    [Fact]
    public void GivenNullServices_WhenAdding_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CoreServiceCollectionExtensions.AddBlazwizCoreServices(null!));
        Assert.Throws<ArgumentNullException>(() => CoreServiceCollectionExtensions.AddBlazwizApp(null!, "acmeportal", AppId));
        Assert.Throws<ArgumentNullException>(() => CoreServiceCollectionExtensions.GetBlazwizApp(null!));
    }

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
        // Act & Assert
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBlazwizApp(name, AppId)).ParamName);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GivenInvalidId_WhenAddBlazwizApp_ThenThrowsArgumentException(string id)
    {
        // Act & Assert
        Assert.Equal("id", Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBlazwizApp("acmeportal", id)).ParamName);
    }

    [Fact]
    public void GivenAppAdded_WhenGetBlazwizApp_ThenItsIdentity()
    {
        // Arrange
        var services = new ServiceCollection().AddBlazwizApp("acmeportal", AppId);

        // Act
        var app = services.GetBlazwizApp();

        // Assert
        Assert.Equal("acmeportal", app.Name);
        Assert.Equal(AppId, app.Id);
    }

    [Fact]
    public void GivenAppNotAdded_WhenGetBlazwizApp_ThenThrowsInvalidOperationException()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().GetBlazwizApp());

        // Assert
        Assert.Contains("AddBlazwizApp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenCoreServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizCoreServices();

        // Assert
        Assert.Same(services, result);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.IsType<OfflineModeService>(provider.GetRequiredService<IOfflineModeService>());
    }
}
