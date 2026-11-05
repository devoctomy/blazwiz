using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// The signed in user's protected profiles as sync profiles: each is a sync profile of its own, named by its id
/// (a GUID), syncing <c>profiles/&lt;id&gt;/</c> with its own keys and its own local storage. Adds them at sign in,
/// creates and deletes them, and removes them at sign out. Unlocking and locking is done with
/// <see cref="Devoctomy.Blazwiz.Encryption.Interfaces.IE2eProtectedProfileManager"/>.
/// </summary>
public interface ISyncProtectedProfileService
{
    /// <summary>
    /// Adds the user's protected profiles as sync profiles, from their index (or this device's copy of it), and
    /// removes any deleted elsewhere. Called when the session starts.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The profiles' scopes.</returns>
    Task<IReadOnlyList<E2eKeyScope>> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a protected profile with its own passphrase and adds it as a sync profile. It is unlocked.
    /// </summary>
    /// <param name="passphrase">The profile's passphrase.</param>
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new profile's scope; its id is the sync profile's name.</returns>
    Task<E2eKeyScope> CreateAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Gets whether a profile has its keys yet: a profile set up in code has none until the user sets its passphrase.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True when the profile has its keys.</returns>
    Task<bool> IsSetUpAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Sets the passphrase of a profile set up in code the first time, creating its keys with its settings. It is
    /// unlocked.
    /// </summary>
    /// <param name="scope">The profile's scope (its id, from <c>UseKeyId</c>).</param>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetUpAsync(E2eKeyScope scope, string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Changes a profile's passphrase, with its passphrase rules and Argon2id costs.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, CancellationToken ct = default);

    /// <summary>
    /// Deletes a protected profile: its keys first, so nothing left behind can be decrypted, then its files in S3 and
    /// on this device, under its sync lock, and then the sync profile.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Lets this device unlock a protected profile with the passkey, when its settings allow it
    /// (<see cref="E2eProfileSettings.AllowDeviceUnlock"/>). The profile must be unlocked.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The profile's settings do not allow device unlock.</exception>
    Task EnableDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Unlocks a protected profile with the passkey, when its settings allow it and it was turned on on this device.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The profile's settings do not allow device unlock.</exception>
    Task UnlockWithDeviceAsync(E2eKeyScope scope);

    /// <summary>
    /// Locks every profile, and removes those the user created as sync profiles. Called when the user signs out.
    /// </summary>
    void Unload();
}
