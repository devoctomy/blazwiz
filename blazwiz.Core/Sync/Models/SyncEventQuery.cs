namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Which sync log entries to read. Results are newest first.
/// </summary>
public class SyncEventQuery
{
    /// <summary>
    /// Gets or sets the profile to include, or null for all.
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets the kinds to include, or null for all.
    /// </summary>
    public IReadOnlyCollection<SyncEventKind>? Kinds { get; set; }

    /// <summary>
    /// Gets or sets text that the message, key, detail or profile must contain (case insensitive), or null.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the earliest time to include, or null.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets or sets the latest time to include, or null.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets or sets how many matching entries to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the most entries to return.
    /// </summary>
    public int Take { get; set; } = 200;
}
