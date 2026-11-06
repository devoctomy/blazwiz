using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Finds the file stores of profiles configured with more than one storage (<c>UseFileStores</c>), and which one the
/// signed in user's profile uses on this device.
/// </summary>
public interface ISyncStorageResolver
{
    /// <summary>
    /// Gets whether a profile can switch storage.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>True if it was configured with <c>UseFileStores</c>.</returns>
    bool IsSwitchable(string profile);

    /// <summary>
    /// Gets the storages configured for a profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The storages, in the order they were added; empty if the profile cannot switch.</returns>
    IReadOnlyList<SyncStorageKind> GetKinds(string profile);

    /// <summary>
    /// Gets a profile's file store for a storage, without change tracking.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="kind">The storage.</param>
    /// <returns>The file store.</returns>
    ISyncFileStore GetStore(string profile, SyncStorageKind kind);

    /// <summary>
    /// Gets the signed in user's state for a profile: the recorded one; otherwise the storage chosen for this device
    /// (<see cref="SetDeviceDefaultKindAsync"/>), if the profile can use it; otherwise the profile's default storage.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The state.</returns>
    Task<SyncStorageState> GetStateAsync(string profile);

    /// <summary>
    /// Gets the storage the signed in user chose for this device: used by profiles that have no storage of their own
    /// recorded yet, such as one the user creates later.
    /// </summary>
    /// <returns>The storage, or null if none was chosen.</returns>
    Task<SyncStorageKind?> GetDeviceDefaultKindAsync();

    /// <summary>
    /// Records the storage the signed in user chose for this device, for profiles that have no storage of their own
    /// recorded yet. Profiles already using a storage are not moved: see <see cref="ISyncStorageMigrationService"/>.
    /// </summary>
    /// <param name="kind">The storage.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetDeviceDefaultKindAsync(SyncStorageKind kind);

    /// <summary>
    /// Gets whether the browser can use a storage.
    /// </summary>
    /// <param name="kind">The storage.</param>
    /// <returns>True if it can.</returns>
    Task<bool> IsSupportedAsync(SyncStorageKind kind);
}
