using System.Reflection;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.UI.Extensions;

/// <summary>
/// Extension points of the app shell: what modules and the app add to the layout, the sign-in page, the Settings
/// page and the sidebar, and which component each framework page shows.
/// </summary>
public static class ShellServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services behind the extension points. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Sidebar items can take their text from resources.
        services.AddBlazwizText();
        services.TryAddSingleton<IShellRegistry, ShellRegistry>();
        services.TryAddSingleton<ISettingsSectionRegistry, SettingsSectionRegistry>();
        services.TryAddSingleton<IPageRegistry, PageRegistry>();
        services.TryAddSingleton<INavigationMenuService, NavigationMenuService>();
        services.TryAddScoped<IShellContentService, ShellContentService>();
        return services;
    }

    /// <summary>
    /// Shows a component in a region of the app shell.
    /// </summary>
    /// <typeparam name="TComponent">The component.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="region">Where it is shown.</param>
    /// <param name="order">Its position in the region: lower comes first.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddShellComponent<TComponent>(this IServiceCollection services, ShellRegion region, int order = 0)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new ShellComponentRegistration(region, typeof(TComponent), order));
        return services;
    }

    /// <summary>
    /// Adds a section to the Settings page.
    /// </summary>
    /// <typeparam name="TComponent">The component showing the section.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="order">Its position on the page: lower comes first.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddSettingsSection<TComponent>(this IServiceCollection services, int order)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new SettingsSectionRegistration(typeof(TComponent), order));
        return services;
    }

    /// <summary>
    /// Adds an item to the sidebar. Its <see cref="NavigationItem.Order"/> sets its position.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="item">The item.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNavigationItem(this IServiceCollection services, NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(item);
        services.AddSingleton(item);
        return services;
    }

    /// <summary>
    /// Adds an option to the sign-in page's advanced options.
    /// </summary>
    /// <typeparam name="TOption">The option. The component gets the same instance by injecting this type.</typeparam>
    /// <typeparam name="TComponent">The component that shows it.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddSignInOption<TOption, TComponent>(this IServiceCollection services)
        where TOption : class, ISignInOption
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<TOption>();
        services.AddScoped<ISignInOption>(sp => sp.GetRequiredService<TOption>());
        services.AddSingleton(new SignInOptionRegistration(typeof(TOption), typeof(TComponent)));
        return services;
    }

    /// <summary>
    /// Sets the component a framework page shows. Used by the framework for its own pages, and by apps to replace one:
    /// the last call for a page wins.
    /// </summary>
    /// <typeparam name="TComponent">The component.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The page, one of <see cref="BlazwizPages"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection ReplacePage<TComponent>(this IServiceCollection services, string key)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        services.AddSingleton(new PageRegistration(key, typeof(TComponent)));
        return services;
    }

    /// <summary>
    /// Adds an assembly with routable pages (<c>@page</c>) for the router to find, e.g. a module's. The app's own
    /// assembly and the framework's are always searched.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddPageAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        if (!services.Any(d => d.ImplementationInstance is PageAssemblyRegistration r && r.Assembly == assembly))
        {
            services.AddSingleton(new PageAssemblyRegistration(assembly));
        }

        return services;
    }

    /// <summary>
    /// Turns a framework page off: its route shows Not Found, and sidebar items linking to it are hidden.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The page, one of <see cref="BlazwizPages"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection DisablePage(this IServiceCollection services, string key)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        services.AddSingleton(new PageRegistration(key, null));
        return services;
    }
}
