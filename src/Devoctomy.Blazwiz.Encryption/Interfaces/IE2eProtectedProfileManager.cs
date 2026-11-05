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
    /// <param name="progress">Told about each step.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new profile's scope; its id is a GUID.</returns>
    Task<E2eKeyScope> CreateAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

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
    /// Gets whether a protected profile is unlocked in this session.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when unlocked.</returns>
    bool IsUnlocked(E2eKeyScope scope);

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
    /// Changes a protected profile's passphrase. Its files are not encrypted again. Needs the keys document to be
    /// reached; the last write wins.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The current passphrase is wrong.</exception>
    /// <exception cref="Exceptions.E2eProfileNotFoundException">The user has no such protected profile.</exception>
    Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, CancellationToken ct = default);

    /// <summary>
    /// Deletes a protected profile's keys: locks it, deletes its keys document and this device's copy, and removes it
    /// from the index. Its files can never be decrypted again; deleting them is up to the caller.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default);
}
