using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// The passphrase of the profiles set up in code, as the Settings section and banner show it until each profile has
/// its own screens: every profile has its own keys, and this sets, unlocks and changes them all with one passphrase.
/// </summary>
public interface ISyncEncryptionService
{
    /// <summary>
    /// Raised when <see cref="Status"/> changes.
    /// </summary>
    event EventHandler<E2eStatus>? StatusChanged;

    /// <summary>
    /// Gets where the profiles set up in code stand: <see cref="E2eStatus.Disabled"/> until their passphrase is set,
    /// <see cref="E2eStatus.NeedsSetup"/> while locked, <see cref="E2eStatus.Unlocked"/> once unlocked, and
    /// <see cref="E2eStatus.Unsupported"/> when their keys cannot be reached in this session.
    /// </summary>
    E2eStatus Status { get; }

    /// <summary>
    /// Works out the status. Call once the user's profiles are loaded.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The status.</returns>
    Task<E2eStatus> InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the passphrase of each profile set up in code that has none yet, creating its keys. They are unlocked.
    /// </summary>
    /// <param name="passphrase">The passphrase. There is no recovery if it is lost.</param>
    /// <param name="progress">Reports each step as it starts and completes.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Devoctomy.Blazwiz.Encryption.Exceptions.E2ePassphraseTooShortException">The passphrase is too short.</exception>
    Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Unlocks the profiles set up in code with their passphrase.
    /// </summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Devoctomy.Blazwiz.Encryption.Exceptions.E2eWrongPassphraseException">Wrong passphrase; the user can try again.</exception>
    Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default);

    /// <summary>
    /// Changes the passphrase of the profiles set up in code. Files and other devices are unaffected.
    /// </summary>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    /// <exception cref="Devoctomy.Blazwiz.Encryption.Exceptions.E2eWrongPassphraseException">The current passphrase is wrong.</exception>
    Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default);

    /// <summary>
    /// Gets whether this session can decrypt files encrypted with a key of a profile set up in code.
    /// </summary>
    /// <param name="keyId">The key id.</param>
    /// <returns>True when the key is unlocked.</returns>
    bool CanDecrypt(string keyId);
}
