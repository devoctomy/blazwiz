namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A past version of a synced file, downloaded and decrypted. Kept in memory only, by whoever shows it.
/// </summary>
public class SyncFileVersionContent
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public SyncFileVersion Version { get; set; } = new SyncFileVersion();

    /// <summary>
    /// Gets or sets the version's content (plaintext).
    /// </summary>
    public byte[] Content { get; set; } = [];
}
