using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The keys of the signed in user's protected profiles: profiles with their own passphrase, each its own key scope
/// identified by a GUID. Creates, lists, unlocks, locks, changes the passphrase of and deletes them. A protected
/// profile is always encrypted, and starts locked in every session.
/// </summary>
public interface IE2eProtectedProfileManager
{
    /// <summary>
    /// Raised when a protected profile is unlocked or locked, with its scope.
    /// </summary>
    event EventHandler<E2eKeyScope>? LockStateChanged;

    /// <summary>
    /// Gets the protected profiles unlocked in this session.
    /// </summary>
    IReadOnlyCollection<E2eKeyScope> Unlocked { get; }

    /// <summary>
    /// Gets the user's protected profiles from their index, or from this device's copy when the index cannot be
    /// reached.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The profiles, in the order they were created.</returns>
    /// <exception cref="InvalidOperationException">The keys cannot be used in this session.</exception>
    /// <exception cref="InvalidDataException">The index is not valid.</exception>
    Task<IReadOnlyList<E2eProfileIndexEntry>> GetProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a protected profile: a new id, a keys document protected by the passphrase, and an entry in the index.
    /// The new profile is unlocked.
    /// </summary>
    /// <param name="passphrase">The profile's passphrase.</param>
    /// <param name="settings">The profile's settings: its passphrase rules and Argon2id costs apply here.</param>
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new profile's scope; its id is a new GUID.</returns>
    /// <exception cref="Exceptions.E2ePassphraseTooShortException">The passphrase is too short.</exception>
    Task<E2eKeyScope> CreateAsync(string passphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Creates the keys of a protected profile whose id is already known (e.g. set in code): a keys document protected
    /// by the passphrase, and an entry in the index. The profile is unlocked.
    /// </summary>
    /// <param name="scope">The profile's scope; its id is a GUID.</param>
    /// <param name="passphrase">The profile's passphrase.</param>
    /// <param name="settings">The profile's settings: its passphrase rules and Argon2id costs apply here.</param>
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2ePassphraseTooShortException">The passphrase is too short.</exception>
    /// <exception cref="InvalidOperationException">The profile already has keys.</exception>
    Task CreateKeysAsync(E2eKeyScope scope, string passphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Unlocks a protected profile with its passphrase, from its keys document or, when that cannot be reached, from
    /// this device's copy of it.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="passphrase">The profile's passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The passphrase is wrong.</exception>
    /// <exception cref="Exceptions.E2eProfileNotFoundException">The user has no such protected profile.</exception>
    Task UnlockAsync(E2eKeyScope scope, string passphrase, CancellationToken ct = default);

    /// <summary>
    /// Lets this device unlock a protected profile with the passkey instead of the passphrase: its master key is wrapped
    /// by this device's key encryption key (from the passkey PRF) and kept on this device only. The profile must be
    /// unlocked, and the user signed in with the passkey.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eLockedException">The profile is locked.</exception>
    /// <exception cref="InvalidOperationException">The user did not sign in with the passkey.</exception>
    Task EnableDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Stops this device unlocking a protected profile with the passkey, deleting its wrapped master key.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisableDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Gets whether device unlock is on for a protected profile on this device: its master key is kept here wrapped by
    /// the passkey's key. Unlike <see cref="CanUnlockWithDeviceAsync"/>, the passkey's key need not be in memory now.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when device unlock is on for it here.</returns>
    Task<bool> HasDeviceUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Gets whether this device can unlock a protected profile with the passkey now.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when device unlock is on for it here and the passkey's key is available.</returns>
    Task<bool> CanUnlockWithDeviceAsync(E2eKeyScope scope);

    /// <summary>
    /// Unlocks a protected profile with the passkey. Its keys document is read from the store when it can be reached;
    /// if it has changed since this device last read it and the master key kept here no longer unwraps it (the keys
    /// were rotated on another device), device unlock is turned off here and the new passphrase is needed. When the
    /// store cannot be reached, this device's copy of the document is used.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2ePassphraseRequiredException">Device unlock is off here, or the passkey cannot
    /// unlock it; the passphrase is needed. Its code is <see cref="Exceptions.E2eErrorCodes.KeysChanged"/> when the
    /// keys were rotated on another device.</exception>
    Task UnlockWithDeviceAsync(E2eKeyScope scope);

    /// <summary>
    /// Gets whether a protected profile is unlocked in this session.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when unlocked.</returns>
    bool IsUnlocked(E2eKeyScope scope);

    /// <summary>
    /// Gets whether an unlocked protected profile's keys document says a passphrase change has not finished: its keys
    /// were rotated, but its files may not all be encrypted with the new key yet, or every device not yet signed out.
    /// The profile must not be used until the change is finished. Known from the document read when it was unlocked or
    /// its passphrase changed; false while it is locked.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when the passphrase change must be finished first.</returns>
    bool IsRotationInProgress(E2eKeyScope scope);

    /// <summary>
    /// Locks a protected profile: its keys are wiped from memory and its files can no longer be read or written.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    void Lock(E2eKeyScope scope);

    /// <summary>
    /// Locks every protected profile unlocked in this session.
    /// </summary>
    void LockAll();

    /// <summary>
    /// Changes a protected profile's passphrase and rotates its keys: a new master key, wrapped by the new passphrase,
    /// and a new active file key. The earlier file keys are kept, retired, so files not yet encrypted again still
    /// decrypt. The document is marked <see cref="UserKeysDocument.RotationInProgress"/> until
    /// <see cref="CompleteRotationAsync"/>, once every file is encrypted with the new key and every device signed
    /// out. The profile is unlocked with the new keys. This device's passkey copy of the master key is replaced when the
    /// passkey's key is in memory, and otherwise removed. Needs the keys document to be reached; a write by another
    /// device in between starts it again, with new keys, from their version.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="settings">The profile's settings: its passphrase rules and Argon2id costs apply here.</param>
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The current passphrase is wrong.</exception>
    /// <exception cref="Exceptions.E2eProfileNotFoundException">The user has no such protected profile.</exception>
    /// <exception cref="Exceptions.E2ePassphraseTooShortException">The new passphrase is too short.</exception>
    Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Clears the <see cref="UserKeysDocument.RotationInProgress"/> mark of an unlocked protected profile, once its
    /// files are all encrypted with the new key. Does nothing when the document's active key is no longer the one this
    /// device holds (rotated again elsewhere since), so that rotation is still finished.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eLockedException">The profile is locked.</exception>
    /// <exception cref="Exceptions.E2eProfileNotFoundException">The user has no such protected profile.</exception>
    Task CompleteRotationAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Deletes a protected profile's keys: locks it, deletes its keys document and this device's copy, and removes it
    /// from the index. Its files can never be decrypted again; deleting them is up to the caller.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Sets how long a protected profile stays unlocked without being used, on every device: it is kept in the index of
    /// the user's profiles (<see cref="E2eProfileIndexEntry.IdleTimeoutSeconds"/>).
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="idleTimeout">The idle timeout, of at least a second; null to never lock it for being idle.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eProfileNotFoundException">The user has no such profile.</exception>
    Task SetIdleTimeoutAsync(E2eKeyScope scope, TimeSpan? idleTimeout, CancellationToken ct = default);

    /// <summary>
    /// Forgets a protected profile deleted on another device: locks it and deletes this device's copy of its keys.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgetAsync(E2eKeyScope scope);

    /// <summary>
    /// Checks whether a protected profile missing from the index still has its keys document, and puts it back in the
    /// index if so. Two devices writing the index at once can lose an entry; the keys document is what makes a profile
    /// real, so a profile is only forgotten once that is gone too.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// True when the profile still has its keys (and is in the index again), or when the keys store could not be
    /// reached, so nothing is known; false when its keys are gone and it can be forgotten.
    /// </returns>
    Task<bool> TryRestoreToIndexAsync(E2eKeyScope scope, CancellationToken ct = default);
}
