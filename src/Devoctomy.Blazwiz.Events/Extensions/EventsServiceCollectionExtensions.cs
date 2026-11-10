using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Events.Channels;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Events.Extensions;

/// <summary>
/// Extension methods for adding user events.
/// </summary>
public static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Adds user events over the local channel (<see cref="LocalUserEventChannel"/>), which reaches the user's other
    /// tabs in this browser only: for development and tests. Use <c>AddBlazwizAppSyncEvents</c> (in
    /// <c>Devoctomy.Blazwiz.Events.AppSync</c>) to reach every device. Needs <c>AddBlazwizApp</c> first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizEvents(this IServiceCollection services)
    {
        return services.AddBlazwizEvents<LocalUserEventChannel>();
    }

    /// <summary>
    /// Adds user events over the given channel: connects it with the session, ends the session when another device
    /// signs the user out everywhere, and tells the other devices when this one does (which also shows "Sign out
    /// everywhere" on the Profile page). The first channel added wins. Needs <c>AddBlazwizApp</c> first.
    /// </summary>
    /// <typeparam name="TChannel">The channel.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizEvents<TChannel>(this IServiceCollection services)
        where TChannel : class, IUserEventChannel
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddBlazwizAuth();
        services.TryAddScoped<IUserEventChannel, TChannel>();
        services.AddSessionLifecycleHandler<UserEventsSessionHandler>();
        services.AddSignOutEverywhereHandler<UserEventsSignOutHandler>();
        return services;
    }
}
