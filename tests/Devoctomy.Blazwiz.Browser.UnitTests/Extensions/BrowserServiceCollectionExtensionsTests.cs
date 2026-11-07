using Devoctomy.Blazwiz.TestSupport;
using Devoctomy.Blazwiz.Browser.Extensions;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Extensions;

public class BrowserServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizBrowser_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => BrowserServiceCollectionExtensions.AddBlazwizBrowser(null!));
    }

    [Fact]
    public void GivenApp_WhenAddBlazwizBrowser_ThenBrowserServicesAreResolvable()
    {
        // Arrange
        var services = TestServices.Create();
        services.AddBlazwizPrfKeyDerivation(prf => prf.UseApplication(TestServices.AppName, TestServices.AppId));

        // Act
        var result = services.AddBlazwizBrowser();

        // Assert
        Assert.Same(services, result);
        using var provider = services.BuildValidated();
        using var scope = provider.CreateScope();
        Assert.IsType<LocalStorageService>(scope.ServiceProvider.GetRequiredService<ILocalStorageService>());
        Assert.IsType<NavigationService>(scope.ServiceProvider.GetRequiredService<INavigationService>());
        Assert.IsType<BrowserInteropService>(scope.ServiceProvider.GetRequiredService<IBrowserInteropService>());
        Assert.IsType<WebAuthnService>(scope.ServiceProvider.GetRequiredService<IWebAuthnService>());
        Assert.IsType<ClipboardService>(scope.ServiceProvider.GetRequiredService<IClipboardService>());
    }
}
