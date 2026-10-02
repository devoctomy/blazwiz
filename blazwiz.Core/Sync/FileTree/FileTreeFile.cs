namespace blazwiz.Core.Sync.FileTree;

/// <summary>
/// A file read from a directory tree (OPFS or a picked folder).
/// </summary>
public class FileTreeFile
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public byte[]? Content { get; set; }

    /// <summary>
    /// Gets or sets when the browser last wrote the file, in milliseconds since the Unix epoch.
    /// </summary>
    public long LastModified { get; set; }
}
