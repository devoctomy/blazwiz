using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// One storage a profile can keep its files in, added by <see cref="SyncFileStoresBuilder"/>.
/// </summary>
/// <param name="kind">The storage.</param>
/// <param name="factory">Creates the file store, without change tracking.</param>
public class SyncStorageOptions(SyncStorageKind kind, Func<IServiceProvider, ISyncFileStore> factory)
{
    /// <summary>
    /// Gets the storage.
    /// </summary>
    public SyncStorageKind Kind { get; } = kind;

    /// <summary>
    /// Gets the factory for the file store, without change tracking.
    /// </summary>
    public Func<IServiceProvider, ISyncFileStore> Factory { get; } = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <summary>
    /// Gets or sets where on the device the storage keeps the profile's files and their metadata (see
    /// <see cref="SyncProfileOptions.DeviceStoreLocations"/>), so that two profiles cannot be set up to share them.
    /// </summary>
    internal IReadOnlyList<string> Locations { get; set; } = [];
}
