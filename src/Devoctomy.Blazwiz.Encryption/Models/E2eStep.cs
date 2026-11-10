namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A step of setting a profile's passphrase, or of changing it (which rotates its keys and encrypts its files again),
/// shown to the user as it happens.
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

    /// <summary>
    /// Encrypting the profile's files again with the new active key, on this device, after a passphrase change.
    /// Reported with how many files are done.
    /// </summary>
    ReencryptFiles,

    /// <summary>
    /// Uploading the files encrypted again. Reported with how many uploads are done.
    /// </summary>
    UploadFiles,
}
