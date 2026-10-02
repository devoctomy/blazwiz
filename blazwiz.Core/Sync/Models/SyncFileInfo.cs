namespace blazwiz.Core.Sync.Models;

/// <summary>
/// A file (or folder marker) in a local file store, without its content.
/// </summary>
public class SyncFileInfo
{
    /// <summary>
    /// Gets or sets the key: the path relative to the user's root, e.g. <c>notes/Work/Ideas.md</c>. Folder markers end with <c>/</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the file was last changed.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the size of the content in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a folder marker (the key ends with <c>/</c>).
    /// </summary>
    public bool IsFolder => Key.EndsWith('/');
}
