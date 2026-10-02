using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Configuration;

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
}
