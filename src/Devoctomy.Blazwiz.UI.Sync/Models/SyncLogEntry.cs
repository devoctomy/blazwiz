namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// A sync log entry ready to show on the Sync page.
/// </summary>
public class SyncLogEntry
{
    /// <summary>
    /// Gets or sets the entry's id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the entry's kind is shown.
    /// </summary>
    public SyncEventPresentation Presentation { get; set; } = new SyncEventPresentation();

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file key, for file entries.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets extra information, e.g. the error.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the time relative to now, e.g. "5 min ago".
    /// </summary>
    public string RelativeTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact local time, for the tooltip.
    /// </summary>
    public string ExactTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many collapsed passes the entry stands for (1 for most entries).
    /// </summary>
    public int Count { get; set; } = 1;
}
