using blazwiz.Core.Cryptography.Models;

namespace blazwiz.Core.Encryption.Models;

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
}
