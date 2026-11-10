using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// What one device keeps for the signed in user so it can unlock without the passphrase. Never leaves the device.
/// </summary>
public class E2eLocalKeys
{
    /// <summary>
    /// Gets or sets the master key, wrapped by this device's key encryption key (derived from the passkey PRF).
    /// </summary>
    public WrappedKey WrappedMasterKey { get; set; } = new WrappedKey();

    /// <summary>
    /// Gets or sets a copy of the keys document (keys.json) as last read, so files can be read offline. Everything in
    /// it is already wrapped, so it is kept as it is.
    /// </summary>
    public string? CachedDocument { get; set; }

    /// <summary>
    /// Gets or sets the store's version (<see cref="StoredDocument.ETag"/>) of <see cref="CachedDocument"/>, so this
    /// device can tell whether the keys document has changed since it last read it. Null when the copy was not read
    /// from the store with a version.
    /// </summary>
    public string? CachedDocumentETag { get; set; }
}
