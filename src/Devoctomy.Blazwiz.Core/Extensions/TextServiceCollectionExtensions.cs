using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Core.Extensions;

/// <summary>
/// Extension methods for the framework's text: every package's UI text comes from resources the app can override and
/// translate.
/// </summary>
public static class TextServiceCollectionExtensions
{
    /// <summary>
    /// Adds <c>IStringLocalizer&lt;T&gt;</c> over each package's resources, with the app's overrides on top. Safe to
    /// call more than once: every call configures the same options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the app's overrides, plural rules and cultures. Optional.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizText(this IServiceCollection services, Action<TextBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services
            .Where(d => d.ServiceType == typeof(TextOptions))
            .Select(d => d.ImplementationInstance)
            .OfType<TextOptions>()
            .FirstOrDefault();
        if (options == null)
        {
            options = new TextOptions();
            services.AddSingleton(options);
        }

        configure?.Invoke(new TextBuilder(options));

        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, OverridingStringLocalizerFactory>());
        services.TryAdd(ServiceDescriptor.Transient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>)));
        services.TryAddSingleton<ITextPlurals, TextPlurals>();
        return services;
    }
}
