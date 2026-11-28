namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// One version of an object in the remote store (S3 versioning), without its content.
/// </summary>
public class RemoteObjectVersion
{
    /// <summary>
    /// Gets or sets the key, relative to the user's root.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version id. Opaque.
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version's ETag. Opaque.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the version was uploaded (S3's <c>LastModified</c>).
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the object's current version.
    /// </summary>
    public bool IsLatest { get; set; }
}
