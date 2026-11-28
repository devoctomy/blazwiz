namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// One version of a synced file in the bucket, without its content: what a file's history lists.
/// </summary>
public class SyncFileVersion
{
    /// <summary>
    /// Gets or sets the profile the file is in.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file's key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version id. Opaque.
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version's ETag, as S3 gives it (in quotes). Opaque: it tells versions apart.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the version was uploaded.
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// Gets or sets the stored (encrypted) size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the file's current version in the bucket.
    /// </summary>
    public bool IsLatest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the version this device last synced.
    /// </summary>
    public bool IsSynced { get; set; }
}
