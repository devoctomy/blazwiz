namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// The sync log rows for one day.
/// </summary>
public class SyncLogDay
{
    /// <summary>
    /// Gets or sets the local date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the heading, e.g. "Today", "Yesterday" or "Monday 28 September 2026".
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rows, newest first.
    /// </summary>
    public IReadOnlyList<SyncLogRow> Rows { get; set; } = [];
}
