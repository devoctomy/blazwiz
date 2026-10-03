using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Time;
using blazwiz.Core.Time.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace blazwiz.Core.Extensions;

/// <summary>
/// Extension methods for registering end to end encryption key management in an <see cref="IServiceCollection"/>.
/// </summary>
public static class E2eEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Adds end to end encryption key management (<see cref="IE2eKeyManager"/>) and the encrypted file format.
    /// </summary>
    /// <remarks>
    /// The app also registers an <see cref="IUserKeysStore"/> (where keys.json is kept), an
    /// <see cref="IE2eLocalKeyStore"/> (this device's wrapped master key), and derives the
    /// <see cref="E2eKeyNames.KeyEncryptionKey"/> from the passkey PRF with <c>AddBlazwizPrfKeyDerivation</c>. Sync
    /// profiles opt in with <c>UseEncryption</c>.
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Changes the Argon2id settings for new keys, or null for the build defaults.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizE2eEncryption(this IServiceCollection services, Action<Argon2idOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var argon2Options = new Argon2idOptions();
        configure?.Invoke(argon2Options);
        services.RemoveAll<Argon2idOptions>();
        services.AddSingleton(argon2Options);

        services.TryAddSingleton<ISystemClock, SystemClock>();
        services.TryAddSingleton<IAeadCipher, BouncyCastleAesGcmAeadCipher>();
        services.TryAddSingleton<IKeyWrapService, AeadKeyWrapService>();
        services.TryAddSingleton<IArgon2idKeyDerivationService, BouncyCastleArgon2idKeyDerivationService>();
        services.TryAddSingleton<IDerivedKeyStore, InMemoryDerivedKeyStore>();
        services.TryAddSingleton<IEncryptedFileFormat, EncryptedFileFormat>();
        services.TryAddSingleton<IE2eKeyRing, E2eKeyRing>();
        services.TryAddScoped<IE2eKeyManager, E2eKeyManager>();

        return services;
    }
}
