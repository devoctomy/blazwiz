namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A file (or folder marker) in a local file store, with its content.
/// </summary>
public class SyncFile
{
    /// <summary>
    /// Gets or sets the key, see <see cref="SyncFileInfo.Key"/>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content. Empty for folder markers.
    /// </summary>
    public byte[] Content { get; set; } = [];

    /// <summary>
    /// Gets or sets when the file was last changed.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets the file's details without its content.
    /// </summary>
    /// <returns>The file info.</returns>
    public SyncFileInfo ToInfo() => new SyncFileInfo { Key = Key, ModifiedAt = ModifiedAt, Size = Content.Length };
}
