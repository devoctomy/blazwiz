using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

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
    /// Gets the signed in user's state for a profile: the recorded one, or the default storage.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The state.</returns>
    Task<SyncStorageState> GetStateAsync(string profile);

    /// <summary>
    /// Gets whether the browser can use a storage.
    /// </summary>
    /// <param name="kind">The storage.</param>
    /// <returns>True if it can.</returns>
    Task<bool> IsSupportedAsync(SyncStorageKind kind);
}
