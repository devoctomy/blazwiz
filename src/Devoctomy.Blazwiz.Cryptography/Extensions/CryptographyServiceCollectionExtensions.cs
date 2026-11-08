using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Cryptography.Extensions;

/// <summary>
/// Extension methods for registering the cryptography services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class CryptographyServiceCollectionExtensions
{
    /// <summary>
    /// Adds AES-GCM encryption, key wrapping and Argon2id key derivation (BouncyCastle). Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizCryptography(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAeadCipher, BouncyCastleAesGcmAeadCipher>();
        services.TryAddSingleton<IKeyWrapService, AeadKeyWrapService>();
        services.TryAddSingleton<IArgon2idKeyDerivationService, BouncyCastleArgon2idKeyDerivationService>();
        services.TryAddSingleton(new Argon2idOptions());
        return services;
    }
}
