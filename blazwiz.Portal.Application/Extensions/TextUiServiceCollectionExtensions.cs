using blazwiz.Core.Extensions;
using blazwiz.Portal.Application.Text;
using blazwiz.Portal.Application.Text.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace blazwiz.Portal.Application.Extensions;

/// <summary>
/// Extension methods for the UI's text: localized, overridable text and error descriptions.
/// </summary>
public static class TextUiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the framework's text (see <c>AddBlazwizText</c>) and <see cref="IErrorText"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizUiText(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddBlazwizText();
        services.TryAddSingleton<IErrorText, ErrorText>();
        return services;
    }

    /// <summary>
    /// Adds resources holding the text for a module's error codes.
    /// </summary>
    /// <typeparam name="TResource">The class the resources are named after.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddErrorText<TResource>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new ErrorTextSource(typeof(TResource)));
        return services;
    }
}
