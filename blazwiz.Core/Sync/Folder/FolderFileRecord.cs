namespace blazwiz.Core.Sync.Folder;

/// <summary>
/// What <see cref="FolderSyncFileStore"/> keeps in IndexedDB beside each file it writes: the modified time to report,
/// and the time the file system gave the file then, to tell when it has since been changed outside the app. Keyed by
/// owner and key, indexed by owner.
/// </summary>
public class FolderFileRecord
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
    /// Gets or sets when the file was last changed, as the app knows it.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the file system's modified time right after the app wrote the file, in milliseconds since the Unix
    /// epoch. A different time now means the file was changed outside the app.
    /// </summary>
    public long FileLastModified { get; set; }
}
