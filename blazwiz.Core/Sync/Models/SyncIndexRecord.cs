using System.Text.Json.Serialization;

namespace blazwiz.Core.Sync.Models;

/// <summary>
/// The sync index entry for one file: what it looked like the last time both sides agreed, and whether it has
/// changed locally since. Holds no content.
/// </summary>
public class SyncIndexRecord
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the user the file belongs to.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file's key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the content at the last sync. Null if never synced.
    /// </summary>
    public string? BaseHash { get; set; }

    /// <summary>
    /// Gets or sets the remote ETag at the last sync. Null if never synced.
    /// </summary>
    public string? BaseETag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file has been written locally since the last sync.
    /// </summary>
    public bool Dirty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file has been deleted locally (a tombstone), so it is removed
    /// remotely rather than downloaded again.
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Gets or sets when the file was deleted locally, used when a delete and a remote edit compete.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets when the file was last synced.
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Gets or sets a counter increased on every local write or delete. A pass only clears <see cref="Dirty"/>
    /// (or overwrites the local file) if it has not changed since the pass took its snapshot.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets a value indicating whether the file has been synced at least once.
    /// </summary>
    [JsonIgnore]
    public bool HasBase => BaseETag != null;

    /// <summary>
    /// Gets a value indicating whether the file has local changes waiting to be synced.
    /// </summary>
    [JsonIgnore]
    public bool IsPending => Dirty || Deleted;

    /// <summary>
    /// Creates a copy of the record.
    /// </summary>
    /// <returns>The copy.</returns>
    public SyncIndexRecord Clone() => (SyncIndexRecord)MemberwiseClone();
}
