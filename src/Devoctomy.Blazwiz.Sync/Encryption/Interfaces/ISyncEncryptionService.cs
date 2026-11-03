using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// End to end encryption of the synced files: turns it on and off (with the sync, offline mode and file sweeps that
/// needs), sets up other devices, and keeps files encrypted as they arrive. Keys are handled by
/// <see cref="IE2eKeyManager"/>.
/// </summary>
public interface ISyncEncryptionService
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
    /// Gets a value indicating whether this session has the passkey (PRF) keys encryption needs.
    /// </summary>
    bool HasPasskeyKeys { get; }

    /// <summary>
    /// Works out the status and unlocks with this device's keys when set up (no passphrase), then encrypts any files
    /// that arrived unencrypted. Call once signed in, before sync starts.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The status.</returns>
    Task<E2eStatus> InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Turns encryption on: syncs, goes offline, checks it is not already on and no file is marked encrypted, makes and
    /// saves the keys, encrypts every file, then puts offline mode back as it was so the encrypted files sync.
    /// </summary>
    /// <param name="passphrase">The new passphrase. There is no recovery if it is lost.</param>
    /// <param name="progress">Reports each step as it starts and completes.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="E2eAlreadyEnabledException">
    /// Already on, from another device: set up this device instead.
    /// </exception>
    Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Sets this device up when encryption is on but this device has not been set up, then syncs.
    /// </summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="E2eWrongPassphraseException">Wrong passphrase; the user can try again.</exception>
    Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default);

    /// <summary>
    /// Changes the passphrase. Files and other devices are unaffected.
    /// </summary>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    /// <exception cref="E2eWrongPassphraseException">The current passphrase is wrong.</exception>
    Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default);

    /// <summary>
    /// Turns encryption off: checks the passphrase, syncs (online only, so every encrypted file is here), goes offline,
    /// decrypts every file, deletes the keys, then puts offline mode back as it was so the decrypted files sync.
    /// </summary>
    /// <param name="passphrase">The passphrase, to confirm.</param>
    /// <param name="progress">Reports each step as it starts and completes.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="E2eWrongPassphraseException">Wrong passphrase.</exception>
    Task DisableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Once encryption was turned off on another device: decrypts this device's encrypted changes not yet synced (with
    /// its own copy of the keys), forgets this device's keys, and syncs, which brings down the decrypted files. With no
    /// such changes, the keys are not needed, so they are forgotten without being unlocked.
    /// </summary>
    /// <param name="passphrase">The passphrase, used when the passkey cannot unlock this device's keys, or null.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="E2ePassphraseRequiredException">Changes must be decrypted, the passkey cannot unlock the keys and no passphrase was given.</exception>
    /// <exception cref="E2eWrongPassphraseException">Wrong passphrase.</exception>
    Task ResolveDisabledElsewhereAsync(string? passphrase = null, CancellationToken ct = default);

    /// <summary>
    /// Gets whether finishing turning encryption off on this device needs the passphrase: it has encrypted changes not
    /// yet synced, and the passkey cannot unlock its keys.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True when the passphrase is needed.</returns>
    Task<bool> ResolveNeedsPassphraseAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets whether this device can decrypt files encrypted with a key.
    /// </summary>
    /// <param name="keyId">The key id.</param>
    /// <returns>True when the key is unlocked.</returns>
    bool CanDecrypt(string keyId);
}
