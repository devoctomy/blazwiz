namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Everything known about one key at the start of a pass, given to the planner.
/// </summary>
public class SyncPlanItem
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local file, or null if there is none.
    /// </summary>
    public SyncFileInfo? Local { get; set; }

    /// <summary>
    /// Gets or sets the hash of the local content, on a full verify pass. Null otherwise.
    /// </summary>
    public string? LocalHash { get; set; }

    /// <summary>
    /// Gets or sets the remote object, or null if there is none.
    /// </summary>
    public RemoteObjectInfo? Remote { get; set; }

    /// <summary>
    /// Gets or sets the index record, or null if there is none.
    /// </summary>
    public SyncIndexRecord? Record { get; set; }
}
