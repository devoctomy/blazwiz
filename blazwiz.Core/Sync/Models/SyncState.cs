namespace blazwiz.Core.Sync.Models;

/// <summary>
/// What the sync service is doing.
/// </summary>
public enum SyncState
{
    /// <summary>
    /// Not syncing. The last pass (if any) succeeded.
    /// </summary>
    Idle,

    /// <summary>
    /// A pass is running.
    /// </summary>
    Syncing,

    /// <summary>
    /// The last pass failed, see <see cref="SyncStatus.LastErrorCode"/>.
    /// </summary>
    Error,
}
