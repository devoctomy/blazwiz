namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// A step of turning end to end encryption on or off, shown to the user as it happens.
/// </summary>
public enum E2eStep
{
    /// <summary>
    /// Syncing, so every file is on this device first.
    /// </summary>
    Sync,

    /// <summary>
    /// Turning on offline mode, so nothing syncs while files change.
    /// </summary>
    GoOffline,

    /// <summary>
    /// Checking encryption was not already enabled on another device.
    /// </summary>
    CheckExistingKeys,

    /// <summary>
    /// Checking no file is already marked as encrypted.
    /// </summary>
    CheckIndex,

    /// <summary>
    /// Checking the passphrase.
    /// </summary>
    VerifyPassphrase,

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
    /// Keeping the master key on this device, wrapped by the passkey.
    /// </summary>
    StoreDeviceKey,

    /// <summary>
    /// Encrypting every file.
    /// </summary>
    EncryptFiles,

    /// <summary>
    /// Decrypting every file.
    /// </summary>
    DecryptFiles,

    /// <summary>
    /// Deleting the keys document and this device's keys.
    /// </summary>
    DeleteKeys,

    /// <summary>
    /// Turning offline mode back to how it was, so files sync.
    /// </summary>
    GoOnline,
}
