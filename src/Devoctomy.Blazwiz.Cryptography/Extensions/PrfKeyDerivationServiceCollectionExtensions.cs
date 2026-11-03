using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Cryptography.Extensions;

/// <summary>
/// Extension methods for registering WebAuthn PRF key derivation in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PrfKeyDerivationServiceCollectionExtensions
{
    /// <summary>
    /// Adds PRF key derivation and the keys to derive. Can be called more than once, so each feature can add its own
    /// keys; the application must be set once with <see cref="PrfKeyDerivationBuilder.UseApplication"/>.
    /// </summary>
    /// <remarks>
    /// Whatever receives the PRF output passes it to <see cref="IPrfKeyDerivator"/>; the rest of the app injects
    /// <see cref="IDerivedKeyStore"/> and gets its keys by name.
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Sets the application and adds the keys.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizPrfKeyDerivation(this IServiceCollection services, Action<PrfKeyDerivationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = services
            .Where(d => d.ServiceType == typeof(PrfKeyDerivationOptions))
            .Select(d => d.ImplementationInstance)
            .OfType<PrfKeyDerivationOptions>()
            .FirstOrDefault();
        if (options == null)
        {
            options = new PrfKeyDerivationOptions();
            services.AddSingleton(options);
        }

        configure(new PrfKeyDerivationBuilder(options));

        services.TryAddSingleton<IKeyDerivationService, BouncyCastleHkdfKeyDerivationService>();
        services.TryAddSingleton<IDerivedKeyStore, InMemoryDerivedKeyStore>();
        services.TryAddSingleton<IPrfKeyDerivator, PrfKeyDerivator>();

        return services;
    }
}
