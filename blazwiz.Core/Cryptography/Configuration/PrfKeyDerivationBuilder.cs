namespace blazwiz.Core.Cryptography.Configuration;

/// <summary>
/// Configures the keys derived from the WebAuthn PRF output. Used by <c>AddBlazwizPrfKeyDerivation</c>.
/// </summary>
/// <param name="options">The options to configure.</param>
public class PrfKeyDerivationBuilder(PrfKeyDerivationOptions options)
{
    /// <summary>
    /// The default key length in bytes, suitable for AES-256.
    /// </summary>
    public const int DefaultKeyLength = 32;

    /// <summary>
    /// Gets the options being configured.
    /// </summary>
    public PrfKeyDerivationOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Sets the application the keys belong to. Changing either value changes every key, so anything encrypted with
    /// the old keys can no longer be decrypted.
    /// </summary>
    /// <param name="name">The application name.</param>
    /// <param name="id">The application's unique ID, a GUID.</param>
    /// <returns>The builder.</returns>
    public PrfKeyDerivationBuilder UseApplication(string name, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Guid.TryParse(id, out var applicationId) || applicationId == Guid.Empty)
        {
            throw new ArgumentException("The application ID must be a GUID, and not the empty GUID.", nameof(id));
        }

        Options.ApplicationName = name;
        Options.ApplicationId = applicationId;
        return this;
    }

    /// <summary>
    /// Adds a key to derive, which the app then gets from the key store by name.
    /// </summary>
    /// <param name="name">The key name.</param>
    /// <param name="length">The length of the key in bytes.</param>
    /// <returns>The builder.</returns>
    public PrfKeyDerivationBuilder AddKey(string name, int length = DefaultKeyLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, BouncyCastleHkdfKeyDerivationService.MaxKeyLength);
        if (Options.Keys.Any(k => k.Name == name))
        {
            throw new ArgumentException($"The key '{name}' has already been added.", nameof(name));
        }

        Options.Keys.Add(new DerivedKeyOptions { Name = name, Length = length });
        return this;
    }
}
