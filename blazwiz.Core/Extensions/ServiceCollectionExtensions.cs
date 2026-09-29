using blazwiz.Core.Greetings;
using blazwiz.Core.Greetings.Interfaces;
using blazwiz.Core.Time;
using blazwiz.Core.Time.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.Extensions;

/// <summary>
/// Extension methods for registering blazwiz core services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all blazwiz core services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<IGreetingService, GreetingService>();

        return services;
    }
}
