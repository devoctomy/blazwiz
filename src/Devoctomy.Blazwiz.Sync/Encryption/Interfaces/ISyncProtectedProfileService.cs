using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Models;

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
    /// Raised when <see cref="Profiles"/> changes: loaded, a profile created, set up or deleted, or unloaded.
    /// </summary>
    event EventHandler? ProfilesChanged;

    /// <summary>
    /// Gets a value indicating whether the user's profiles are loaded. False until <see cref="LoadAsync"/> succeeds,
    /// and after <see cref="Unload"/>.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets the user's profiles: those set up in code, in the order they were added, then those the user created.
    /// </summary>
    IReadOnlyList<SyncProtectedProfile> Profiles { get; }

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
    /// Deletes a protected profile, under its sync lock: finds its files in S3 (and stops, with nothing deleted, if S3
    /// cannot be reached), then deletes its keys, so nothing left behind can be decrypted, then its files in S3 and on
    /// this device, and then the sync profile. Without a sync target in the session (local development) there are no
    /// files in S3 to delete. Once its keys are deleted the profile is removed even if deleting its files then fails;
    /// that error is still thrown.
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
    /// Gets whether device unlock is on for a protected profile on this device (its settings allow it, and it was
    /// turned on here), whether or not the passkey's key is in memory now.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when it is on here.</returns>
    Task<bool> HasDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Stops this device unlocking a protected profile with the passkey, forgetting its passkey-wrapped key here. The
    /// passphrase still unlocks it.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisableDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Unlocks a protected profile with the passkey, when its settings allow it and it was turned on on this device.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The profile's settings do not allow device unlock.</exception>
    Task UnlockWithDeviceAsync(E2eKeyScope scope);

    /// <summary>
    /// Labels a profile the user created on this device, e.g. with a name the app keeps encrypted inside it, read once
    /// it is unlocked. The label is kept only on this device and never synced; it is shown in place of the profile's
    /// id.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="label">The label.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetLabelAsync(E2eKeyScope scope, string label);

    /// <summary>
    /// Sets how long a profile the user created stays unlocked without being used. It is kept with the user's profiles,
    /// so it applies on every device, and takes effect at once: an unlocked profile is timed again from now.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="idleTimeout">The idle timeout, of at least a second; null to never lock it for being idle.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetIdleTimeoutAsync(E2eKeyScope scope, TimeSpan? idleTimeout, CancellationToken ct = default);

    /// <summary>
    /// Locks every profile, and removes those the user created as sync profiles. Called when the user signs out.
    /// </summary>
    void Unload();
}
