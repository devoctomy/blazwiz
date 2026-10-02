namespace blazwiz.Core.Sync.Models;

/// <summary>
/// A file that changed on both sides, given to the conflict resolver.
/// </summary>
public class SyncConflict
{
    /// <summary>
    /// Gets or sets the file's key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how both sides changed.
    /// </summary>
    public SyncConflictKind Kind { get; set; }

    /// <summary>
    /// Gets or sets when the local side changed: the file's modified time, or when it was deleted.
    /// </summary>
    public DateTimeOffset? LocalChangedAt { get; set; }

    /// <summary>
    /// Gets or sets the remote object, read with a HEAD request. Null when it has been deleted remotely.
    /// </summary>
    public RemoteObjectInfo? Remote { get; set; }
}
