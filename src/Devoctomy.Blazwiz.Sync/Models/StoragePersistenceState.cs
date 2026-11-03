namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// What the app has asked the browser about persistent storage, kept in local storage.
/// </summary>
public class StoragePersistenceState
{
    /// <summary>
    /// Gets or sets a value indicating whether persistent storage has been asked for.
    /// </summary>
    public bool Requested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser granted it.
    /// </summary>
    public bool Granted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the app was installed when the browser last refused.
    /// </summary>
    public bool InstalledWhenRefused { get; set; }
}
