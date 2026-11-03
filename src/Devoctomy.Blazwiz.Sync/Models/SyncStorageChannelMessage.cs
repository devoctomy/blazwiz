namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A message from another tab about the storage files are kept in.
/// </summary>
public enum SyncStorageChannelMessage
{
    /// <summary>
    /// Files are being moved to another storage: changes must wait.
    /// </summary>
    Migrating,

    /// <summary>
    /// The move has finished (or failed): reload to use the storage now selected.
    /// </summary>
    Changed,
}
