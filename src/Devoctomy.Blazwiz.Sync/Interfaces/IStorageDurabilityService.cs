using Devoctomy.Blazwiz.Sync.Storage;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Asks the browser to keep the site's storage (<c>navigator.storage.persist()</c>) and reports whether it does,
/// through <c>wwwroot/js/storage.js</c>.
/// </summary>
public interface IStorageDurabilityService
{
    /// <summary>
    /// Gets whether storage is persistent, how much is used and whether the app is installed.
    /// </summary>
    /// <returns>The status.</returns>
    Task<StorageDurabilityStatus> GetStatusAsync();

    /// <summary>
    /// Asks the browser for persistent storage. Firefox shows a permission prompt; other browsers decide silently.
    /// </summary>
    /// <returns>True if storage is now persistent.</returns>
    Task<bool> RequestPersistenceAsync();
}
