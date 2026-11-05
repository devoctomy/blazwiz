namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A step of setting a profile's passphrase the first time, shown to the user as it happens.
/// </summary>
public enum E2eStep
{
    /// <summary>
    /// Deriving the passphrase key with Argon2id.
    /// </summary>
    DeriveKey,

    /// <summary>
    /// Making the master key and file key.
    /// </summary>
    CreateKeys,

    /// <summary>
    /// Saving the keys document.
    /// </summary>
    SaveKeys,

    /// <summary>
    /// Keeping a copy of the keys document on this device.
    /// </summary>
    StoreDeviceKey,
}
