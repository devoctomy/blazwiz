using blazwiz.Core.Configuration;
using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace blazwiz.Core.Extensions;

/// <summary>
/// Extension methods for registering blazwiz core services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Identifies the app: its name, used to name everything it keeps in the browser, and its unique ID. Call once at
    /// startup, before anything that uses browser storage is resolved.
    /// </summary>
    /// <param name="services">The service collection to add the app's identity to.</param>
    /// <param name="name">The app's name: lower case letters and numbers, starting with a letter.</param>
    /// <param name="id">The app's unique ID, a GUID.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentException">The name or ID is not valid.</exception>
    public static IServiceCollection AddBlazwizApp(this IServiceCollection services, string name, string id)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!char.IsAsciiLetterLower(name[0]) || !name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)))
        {
            throw new ArgumentException("The app name must be lower case letters and numbers, starting with a letter.", nameof(name));
        }

        if (!Guid.TryParse(id, out var parsedId) || parsedId == Guid.Empty)
        {
            throw new ArgumentException("The app ID must be a GUID other than the empty one.", nameof(id));
        }

        services.AddSingleton(new BlazwizAppOptions { Name = name, Id = id });
        services.TryAddSingleton<IAppStorageKeys, AppStorageKeys>();
        return services;
    }

    /// <summary>
    /// Adds all blazwiz core services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOfflineModeService, OfflineModeService>();
        services.AddScoped<ICryptoService, BouncyCastleAesGcmCryptoService>();
        services.TryAddSingleton<IAeadCipher, BouncyCastleAesGcmAeadCipher>();
        services.TryAddSingleton<IKeyWrapService, AeadKeyWrapService>();
        services.TryAddSingleton<IArgon2idKeyDerivationService, BouncyCastleArgon2idKeyDerivationService>();
        services.TryAddSingleton(new Argon2idOptions());

        return services;
    }
}
