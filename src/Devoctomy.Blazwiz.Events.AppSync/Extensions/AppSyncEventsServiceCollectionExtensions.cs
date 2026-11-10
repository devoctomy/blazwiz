using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Events.AppSync.Channels;
using Devoctomy.Blazwiz.Events.AppSync.Models;
using Devoctomy.Blazwiz.Events.AppSync.Sessions;
using Devoctomy.Blazwiz.Events.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Events.AppSync.Extensions;

/// <summary>
/// Extension methods for user events over AWS AppSync Events.
/// </summary>
public static class AppSyncEventsServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section holding the endpoints (<see cref="AppSyncEventsConfig"/>).
    /// </summary>
    public const string ConfigurationSection = "Events";

    /// <summary>
    /// Adds user events over the AppSync Events API in the <c>Events</c> configuration section. When it is not
    /// configured: where <paramref name="allowLocalChannel"/> says so (pass the host environment's
    /// <c>IsDevelopment()</c>), the local channel between this browser's tabs stands in; otherwise the app runs
    /// without events and logs a warning when a session starts. It never falls back to the local channel in a
    /// deployed app, which would look like it works while reaching no other device. Needs <c>AddBlazwizApp</c> first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration.</param>
    /// <param name="allowLocalChannel">Whether the local channel may stand in when AppSync is not configured.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizAppSyncEvents(this IServiceCollection services, IConfiguration configuration, bool allowLocalChannel)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var config = new AppSyncEventsConfig();
        configuration.GetSection(ConfigurationSection).Bind(config);
        if (IsConfigured(config))
        {
            services.AddSingleton(config);
            return services.AddBlazwizEvents<AppSyncUserEventChannel>();
        }

        if (allowLocalChannel)
        {
            return services.AddBlazwizEvents();
        }

        services.AddBlazwizAuth();
        return services.AddSessionLifecycleHandler<MissingEventsConfigurationWarning>();
    }

    /// <summary>
    /// Gets a value indicating whether both endpoints are set.
    /// </summary>
    /// <param name="config">The settings.</param>
    /// <returns>True when the HTTP and real-time endpoints are both set.</returns>
    public static bool IsConfigured(AppSyncEventsConfig? config)
    {
        return config != null
            && !string.IsNullOrWhiteSpace(config.HttpEndpoint)
            && !string.IsNullOrWhiteSpace(config.RealtimeEndpoint);
    }
}
