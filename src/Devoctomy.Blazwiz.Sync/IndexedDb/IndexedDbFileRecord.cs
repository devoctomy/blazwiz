namespace Devoctomy.Blazwiz.Sync.IndexedDb;

/// <summary>
/// A file as kept by <see cref="IndexedDbSyncFileStore"/>: keyed by owner and key, indexed by owner.
/// </summary>
public class IndexedDbFileRecord
{
    /// <summary>
    /// Gets or sets the id of the user the file belongs to.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content, stored as binary. Empty for folder markers.
    /// </summary>
    public byte[]? Content { get; set; }

    /// <summary>
    /// Gets or sets when the file was last changed.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the size of the content in bytes.
    /// </summary>
    public long Size { get; set; }
}
