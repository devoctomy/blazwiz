namespace Devoctomy.Blazwiz.Sync.Storage;

/// <summary>
/// Whether the browser keeps the site's storage (IndexedDB, OPFS, Cache API) when space is short, and how much is used.
/// </summary>
public class StorageDurabilityStatus
{
    /// <summary>
    /// Gets or sets a value indicating whether the browser supports the Storage API.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether storage is persistent rather than best effort.
    /// </summary>
    public bool IsPersisted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the app is installed (running standalone).
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Gets or sets the bytes used, or null if unknown.
    /// </summary>
    public long? UsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the bytes available, or null if unknown.
    /// </summary>
    public long? QuotaBytes { get; set; }
}
