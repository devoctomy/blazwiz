namespace blazwiz.Core.Sync.Opfs;

/// <summary>
/// A file read from OPFS.
/// </summary>
public class OpfsFile
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
