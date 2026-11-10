using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Browser.Extensions;

/// <summary>
/// Extension methods for registering the browser services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class BrowserServiceCollectionExtensions
{
    /// <summary>
    /// Adds local storage (named after the app, see <c>AddBlazwizApp</c>), navigation, browser interop, WebAuthn
    /// passkeys, the clipboard, the device's name and page resume events. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizBrowser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ILocalStorageService, LocalStorageService>();
        services.TryAddScoped<INavigationService, NavigationService>();
        services.TryAddScoped<IBrowserInteropService, BrowserInteropService>();
        services.TryAddScoped<IWebAuthnService, WebAuthnService>();
        services.TryAddScoped<IClipboardService, ClipboardService>();
        services.TryAddScoped<IDeviceNameService, DeviceNameService>();
        services.TryAddScoped<IPageResumeService, PageResumeService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
