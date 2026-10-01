namespace blazwiz.Core.Sync.Models;

/// <summary>
/// How both sides of a file changed since the last sync.
/// </summary>
public enum SyncConflictKind
{
    /// <summary>
    /// Edited on both sides (or created on both sides with different content).
    /// </summary>
    EditEdit,

    /// <summary>
    /// Deleted locally, edited remotely.
    /// </summary>
    DeleteEdit,

    /// <summary>
    /// Edited locally, deleted remotely.
    /// </summary>
    EditDelete,
}
