namespace Devoctomy.Blazwiz.Cryptography.Configuration;

/// <summary>
/// The keys to derive from the WebAuthn PRF output, built by <see cref="PrfKeyDerivationBuilder"/>.
/// </summary>
public class PrfKeyDerivationOptions
{
    /// <summary>
    /// Gets or sets the application name, bound into every key so another app's keys differ.
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application's unique ID (a GUID), used as the salt.
    /// </summary>
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Gets the keys to derive.
    /// </summary>
    public List<DerivedKeyOptions> Keys { get; } = [];
}
