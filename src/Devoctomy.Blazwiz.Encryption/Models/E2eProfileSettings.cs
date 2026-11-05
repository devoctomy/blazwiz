using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A profile's security settings: when it locks itself, whether it can be unlocked with the passkey, its passphrase
/// rules and how its passphrase key is derived. Set in code, for a profile set up in code or as the defaults of the
/// profiles users create.
/// </summary>
public class E2eProfileSettings
{
    /// <summary>
    /// Gets or sets how long the profile stays unlocked without its files being read or written, or null to stay
    /// unlocked until locked or signed out. 15 minutes by default.
    /// </summary>
    public TimeSpan? IdleTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets a value indicating whether the user may unlock the profile with the passkey on a device instead of
    /// the passphrase. Off by default.
    /// </summary>
    public bool AllowDeviceUnlock { get; set; }

    /// <summary>
    /// Gets or sets the shortest passphrase allowed, in characters. 12 by default.
    /// </summary>
    public int MinimumPassphraseLength { get; set; } = 12;

    /// <summary>
    /// Gets or sets the Argon2id costs for deriving the passphrase key, or null for the app's
    /// (<c>AddBlazwizE2eEncryption</c>). Stored in the profile's keys document, so they can be raised later.
    /// </summary>
    public Argon2idParameters? Argon2 { get; set; }
}
