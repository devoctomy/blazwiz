namespace blazwiz.Core.Sync.Opfs;

/// <summary>
/// What <see cref="OpfsSyncFileStore"/> keeps in IndexedDB beside each file: the modified time to report, which OPFS
/// cannot set. Keyed by owner and key, indexed by owner.
/// </summary>
public class OpfsFileRecord
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
    /// Gets or sets when the file was last changed.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
