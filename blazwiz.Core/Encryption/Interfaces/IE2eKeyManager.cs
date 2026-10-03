using blazwiz.Core.Encryption.Models;

namespace blazwiz.Core.Encryption.Interfaces;

/// <summary>
/// Manages the signed in user's end to end encryption keys: the keys document (keys.json) in the
/// <see cref="Cryptography.Interfaces.IUserKeysStore"/>, this device's wrapped master key in the
/// <see cref="IE2eLocalKeyStore"/>, and the unlocked keys in the <see cref="IE2eKeyRing"/>. Knows nothing about files.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Passphrase key: Argon2id of the passphrase. Never stored; only exists during the calls taking a passphrase.</item>
/// <item>Master key: random. Wrapped by the passphrase key in keys.json, and by the PRF key encryption key on each device.</item>
/// <item>File keys: random. Wrapped by the master key in keys.json, by id. These encrypt the files.</item>
/// </list>
/// Everything that needs the passkey (PRF) key encryption key throws <see cref="InvalidOperationException"/> without it.
/// </remarks>
public interface IE2eKeyManager
{
    /// <summary>
    /// Raised when <see cref="Status"/> changes.
    /// </summary>
    event EventHandler<E2eStatus>? StatusChanged;

    /// <summary>
    /// Gets where encryption stands on this device.
    /// </summary>
    E2eStatus Status { get; }

    /// <summary>
    /// Gets a value indicating whether this session has the passkey (PRF) key encryption key, which everything except
    /// checking or changing the passphrase needs.
    /// </summary>
    bool HasKeyEncryptionKey { get; }

    /// <summary>
    /// Works out the status and, when this device is set up, unlocks with its wrapped master key (no passphrase). Reads
    /// keys.json when it can be reached and keeps a copy on this device; offline, uses the copy.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The status.</returns>
    Task<E2eStatus> InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads keys.json afresh to see whether encryption is enabled, e.g. by another device.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True when keys.json exists.</returns>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Enables encryption: derives the passphrase key with a new salt, makes the master and file keys, saves keys.json
    /// (failing rather than replacing one made meanwhile), keeps the master key on this device and unlocks.
    /// </summary>
    /// <param name="passphrase">The new passphrase. There is no recovery if it is lost.</param>
    /// <param name="progress">Reports the steps.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eAlreadyEnabledException">keys.json already exists.</exception>
    Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Sets this device up when encryption is enabled but this device has no usable wrapped master key: unwraps the
    /// master key with the passphrase, keeps it on this device and unlocks.
    /// </summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The passphrase is wrong; the user can try again.</exception>
    Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default);

    /// <summary>
    /// Changes the passphrase: checks the current one, re-wraps the master key with a new salt and passphrase key, and
    /// saves keys.json. The file keys and files are untouched, and other devices keep working.
    /// </summary>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes once keys.json is saved.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The current passphrase is wrong.</exception>
    Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default);

    /// <summary>
    /// Checks the passphrase against keys.json, e.g. to confirm turning encryption off.
    /// </summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The passphrase is wrong.</exception>
    Task VerifyPassphraseAsync(string passphrase, CancellationToken ct = default);

    /// <summary>
    /// When encryption was turned off on another device: unlocks with this device's wrapped master key and its copy of
    /// keys.json, writing plaintext, so local encrypted changes can be decrypted before the keys are forgotten.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task UnlockLocalCopyAsync(CancellationToken ct = default);

    /// <summary>
    /// Disables encryption everywhere: deletes keys.json and this device's keys, and forgets the unlocked keys. Files
    /// must be decrypted first.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RemoveAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets this device's keys and the unlocked keys, leaving keys.json alone, e.g. once encryption was turned off
    /// on another device.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgetDeviceAsync();
}
