using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;

namespace MyApp.Portal.Application.Extensions;

/// <summary>
/// The app's own services and pages, on top of the framework. Add your own here.
/// </summary>
public static class AppServiceCollectionExtensions
{
    /// <summary>
    /// Adds the app's own services, and its pages to the sidebar.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGreetingService, GreetingService>();
        services.AddNavigationItem(new NavigationItem
        {
            Text = "Dashboard",
            Href = string.Empty,
            Icon = "bi-grid-1x2",
            ExactMatch = true,
            Description = "Your home page.",
            Order = 0,
        });
        return services;
    }
}
