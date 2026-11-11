using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Runs sync passes over the configured profiles and reports the status.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Raised when <see cref="Status"/> changes.
    /// </summary>
    event EventHandler<SyncStatus>? StatusChanged;

    /// <summary>
    /// Raised when a pass downloads or deletes local files, so open views can refresh.
    /// </summary>
    event EventHandler<SyncFilesChangedEventArgs>? FilesChanged;

    /// <summary>
    /// Gets the current status.
    /// </summary>
    SyncStatus Status { get; }

    /// <summary>
    /// Gets the names of the configured profiles.
    /// </summary>
    IReadOnlyList<string> Profiles { get; }

    /// <summary>
    /// Runs a pass. Does not check connectivity or take the cross tab lock: the scheduler does that.
    /// </summary>
    /// <param name="profile">The profile, or null for every profile.</param>
    /// <param name="verify">True to hash every local file against the index.</param>
    /// <param name="progress">Told how many of each profile's planned changes are done.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SyncResult> SyncAsync(string? profile = null, bool verify = false, IProgress<SyncPassProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Recounts the local changes waiting to be synced, e.g. while offline.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RefreshPendingCountAsync();

    /// <summary>
    /// Clears the status, e.g. on sign out.
    /// </summary>
    void Reset();
}
