namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A page of sync log entries.
/// </summary>
public class SyncEventPage
{
    /// <summary>
    /// Gets or sets the entries, newest first.
    /// </summary>
    public IReadOnlyList<SyncEvent> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether more entries match after this page.
    /// </summary>
    public bool HasMore { get; set; }
}
