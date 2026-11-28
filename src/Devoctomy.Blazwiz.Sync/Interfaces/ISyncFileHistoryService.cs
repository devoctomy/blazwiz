using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// A synced file's history, from the data bucket's versioning: its past versions are listed (metadata only), one is
/// downloaded and decrypted when it is wanted, and restoring one saves its content as the file's new current version,
/// exactly as app code saving it would. Nothing is ever deleted or overwritten in the bucket.
/// </summary>
public interface ISyncFileHistoryService
{
    /// <summary>
    /// Gets a value indicating whether there is a bucket to keep history in (<see cref="IS3SyncTargetProvider.IsConfigured"/>).
    /// When there is not (local development), there is no history to show.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Lists every version of a file the bucket has, newest first, without downloading any of them.
    /// </summary>
    /// <param name="profile">The profile the file is in.</param>
    /// <param name="key">The file's key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The versions; empty when the file was never uploaded.</returns>
    /// <exception cref="SyncNetworkException">The bucket could not be reached.</exception>
    /// <exception cref="SyncAccessDeniedException">There are no credentials, or they were refused.</exception>
    Task<IReadOnlyList<SyncFileVersion>> GetVersionsAsync(string profile, string key, CancellationToken ct = default);

    /// <summary>
    /// Downloads one version and decrypts it with the profile's keys.
    /// </summary>
    /// <param name="version">The version, from <see cref="GetVersionsAsync"/>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The version's content, or null if the bucket no longer has it.</returns>
    /// <exception cref="SyncNetworkException">The bucket could not be reached.</exception>
    /// <exception cref="SyncAccessDeniedException">There are no credentials, or they were refused.</exception>
    /// <exception cref="E2eLockedException">The profile is locked.</exception>
    Task<SyncFileVersionContent?> ReadVersionAsync(SyncFileVersion version, CancellationToken ct = default);

    /// <summary>
    /// Saves a version's content as the file's new current version, through the profile's file store: encrypted with
    /// the active key, and uploaded by the next sync like any other change (conditionally, so a change made meanwhile on
    /// another device goes to the usual conflict handling). The version it replaces stays in the history. Works offline.
    /// </summary>
    /// <param name="version">The version, from <see cref="ReadVersionAsync"/>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="E2eLockedException">The profile is locked.</exception>
    Task RestoreAsync(SyncFileVersionContent version, CancellationToken ct = default);
}
