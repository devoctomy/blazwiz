using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Encryption.Extensions;

/// <summary>
/// Extension methods for registering end to end encryption key management in an <see cref="IServiceCollection"/>.
/// </summary>
public static class E2eEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Adds end to end encryption key management (<see cref="IE2eKeyManager"/> for the account,
    /// <see cref="IE2eProtectedProfileManager"/> for protected profiles) and the encrypted file format.
    /// </summary>
    /// <remarks>
    /// The app also registers an <see cref="IUserKeysStore"/> (where the keys documents are kept), an
    /// <see cref="IE2eLocalKeyStore"/> (this device's copies), an <see cref="IE2eProfileIndexStore"/> and an
    /// <see cref="IE2eLocalProfileIndexStore"/> (the index of protected profiles), and derives the
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

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAeadCipher, BouncyCastleAesGcmAeadCipher>();
        services.TryAddSingleton<IKeyWrapService, AeadKeyWrapService>();
        services.TryAddSingleton<IArgon2idKeyDerivationService, BouncyCastleArgon2idKeyDerivationService>();
        services.TryAddSingleton<IDerivedKeyStore, InMemoryDerivedKeyStore>();
        services.TryAddSingleton<IEncryptedFileFormat, EncryptedFileFormat>();
        services.TryAddSingleton<IE2eKeysDocumentService, E2eKeysDocumentService>();
        services.TryAddSingleton<IE2eKeyRings, E2eKeyRings>();
        services.TryAddSingleton(sp => sp.GetRequiredService<IE2eKeyRings>().Get(E2eKeyScope.Account));
        services.TryAddScoped<IE2eKeyManager, E2eKeyManager>();
        services.TryAddScoped<IE2eProtectedProfileManager, E2eProtectedProfileManager>();

        return services;
    }
}
