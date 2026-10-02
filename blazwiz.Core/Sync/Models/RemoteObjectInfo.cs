namespace blazwiz.Core.Sync.Models;

/// <summary>
/// An object in the remote store, without its content.
/// </summary>
public class RemoteObjectInfo
{
    /// <summary>
    /// Gets or sets the key, relative to the user's root.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ETag. Opaque: only ever compared for equality.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the object was uploaded (S3's <c>LastModified</c>).
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Gets or sets when the file was last edited, from the <c>x-amz-meta-modified-at</c> metadata. Only known after a
    /// HEAD or GET request, and null when the object was uploaded without it.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets the best known edit time: <see cref="ModifiedAt"/>, or <see cref="LastModified"/> when it is missing.
    /// </summary>
    public DateTimeOffset EditedAt => ModifiedAt ?? LastModified;
}
