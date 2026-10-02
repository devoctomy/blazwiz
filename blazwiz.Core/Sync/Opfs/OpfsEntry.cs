namespace blazwiz.Core.Sync.Opfs;

/// <summary>
/// A file (or folder marker) listed from OPFS.
/// </summary>
public class OpfsEntry
{
    /// <summary>
    /// Gets or sets the key. Folder markers end with <c>/</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets when the browser last wrote the file, in milliseconds since the Unix epoch.
    /// </summary>
    public long LastModified { get; set; }
}
