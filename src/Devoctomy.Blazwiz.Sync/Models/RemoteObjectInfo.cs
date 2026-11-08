namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// An object in the remote store, without its content.
/// </summary>
public class RemoteObjectInfo
{
    /// <summary>
    /// How far ahead of the upload time (<see cref="LastModified"/>) an edit time (<see cref="ModifiedAt"/>) may be and
    /// still be believed: a device's clock may be a little fast. An edit time further ahead was not written by a device
    /// syncing normally, so it is ignored rather than letting it win every conflict.
    /// </summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

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
    /// Gets the best known edit time: <see cref="ModifiedAt"/>, or <see cref="LastModified"/> when it is missing or
    /// more than <see cref="MaxClockSkew"/> after the upload (a file cannot be edited after it was uploaded).
    /// </summary>
    public DateTimeOffset EditedAt
    {
        get
        {
            if (ModifiedAt is not { } modifiedAt)
            {
                return LastModified;
            }

            // No upload time known (e.g. a store that does not record one): nothing to check against.
            if (LastModified == DateTimeOffset.MinValue || modifiedAt <= LastModified + MaxClockSkew)
            {
                return modifiedAt;
            }

            return LastModified;
        }
    }
}
