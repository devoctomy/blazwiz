namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// The planned action for one file, with the snapshot it was planned from.
/// </summary>
public class SyncAction
{
    /// <summary>
    /// Gets or sets the file's key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what to do.
    /// </summary>
    public SyncActionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how both sides changed, when <see cref="Kind"/> is <see cref="SyncActionKind.Conflict"/>.
    /// </summary>
    public SyncConflictKind? ConflictKind { get; set; }

    /// <summary>
    /// Gets or sets the local file, or null if there is none.
    /// </summary>
    public SyncFileInfo? Local { get; set; }

    /// <summary>
    /// Gets or sets the remote object, or null if there is none.
    /// </summary>
    public RemoteObjectInfo? Remote { get; set; }

    /// <summary>
    /// Gets or sets the index record, or null if there is none.
    /// </summary>
    public SyncIndexRecord? Record { get; set; }
}
