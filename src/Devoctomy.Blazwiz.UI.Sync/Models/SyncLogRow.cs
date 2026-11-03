namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// A row in the sync log: one pass with its file entries, or one entry on its own.
/// </summary>
public class SyncLogRow
{
    /// <summary>
    /// Gets or sets the row id (the pass id, or the entry id).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry shown in the row itself.
    /// </summary>
    public SyncLogEntry Header { get; set; } = new SyncLogEntry();

    /// <summary>
    /// Gets or sets the file entries shown when the row is expanded.
    /// </summary>
    public IReadOnlyList<SyncLogEntry> Children { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the row is expanded.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether anything in the row failed.
    /// </summary>
    public bool HasErrors { get; set; }
}
