namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// An entry in the sync log.
/// </summary>
public class SyncEvent
{
    /// <summary>
    /// Gets or sets the unique id.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the id of the user the entry belongs to.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile, or an empty string for entries about the whole app (e.g. connectivity).
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id shared by every entry of one pass, or null for entries outside a pass.
    /// </summary>
    public string? PassId { get; set; }

    /// <summary>
    /// Gets or sets when it happened. For collapsed no-change passes, when the last one finished.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public SyncEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the file's key, for file entries.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets what happened, as a code (see <see cref="SyncEventCodes"/>). The UI shows it with the text resource
    /// <c>Sync.Log.&lt;code&gt;</c>.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the values the code's text needs, e.g. counts or an error's code (see
    /// <see cref="SyncEventCodes"/>).
    /// </summary>
    public List<string> Arguments { get; set; } = [];

    /// <summary>
    /// Gets or sets extra information, e.g. the error, or which side won a conflict.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets how many consecutive no-change passes this entry stands for. 1 for every other entry.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether this is a pass that changed nothing (collapsed with the ones before it).
    /// </summary>
    public bool IsNoChangePass { get; set; }
}
