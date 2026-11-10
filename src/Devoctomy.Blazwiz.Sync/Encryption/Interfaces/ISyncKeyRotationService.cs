using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Changes a protected profile's passphrase so it locks out anyone who had the old one: the profile is synced, its keys
/// rotated, and every file encrypted again with the new key and uploaded. Signing every device out is up to the caller
/// (it needs the session); while it does, <see cref="CompleteAsync"/> clears the rotation mark. A change cut short is
/// finished with <see cref="FinishAsync"/>.
/// </summary>
/// <remarks>
/// Without a sync target in the session (local development) there is nothing to sync or upload: the files on this
/// device are encrypted again.
/// </remarks>
public interface ISyncKeyRotationService
{
    /// <summary>
    /// Gets the profiles whose files this session has encrypted again and uploaded, so their rotation mark can be
    /// cleared once every device is signed out.
    /// </summary>
    IReadOnlyCollection<E2eKeyScope> ReadyToComplete { get; }

    /// <summary>
    /// Gets a profile ready for a passphrase change: syncs it, so this device holds every file, then times encrypting
    /// and uploading a throwaway file (outside the synced files, and deleted again) to estimate how long encrypting them
    /// all again will take. Nothing is changed.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The estimate.</returns>
    /// <exception cref="SyncKeyRotationException">This device is offline, or the profile could not be synced.</exception>
    Task<SyncKeyRotationEstimate> PrepareAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Changes the profile's passphrase: rotates its keys, then encrypts every file again with the new key and uploads
    /// it. Call <see cref="PrepareAsync"/> first. Once the keys are rotated, a failure leaves the change to be finished
    /// with <see cref="FinishAsync"/>.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="newPassphrase">The new passphrase.</param>
    /// <param name="progress">Told about each step, and how many files are done.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="SyncKeyRotationException">This device went offline, or the files could not be uploaded.</exception>
    Task RotateAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Finishes a passphrase change cut short: syncs the unlocked profile, then encrypts again and uploads every file
    /// not yet encrypted with the active key.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <param name="progress">Told how many files are done.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="SyncKeyRotationException">This device is offline, or the profile could not be synced.</exception>
    Task FinishAsync(E2eKeyScope scope, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Clears the rotation mark of every profile in <see cref="ReadyToComplete"/>. Called while every device is signed
    /// out, before this one is.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CompleteAsync(CancellationToken ct = default);
}
