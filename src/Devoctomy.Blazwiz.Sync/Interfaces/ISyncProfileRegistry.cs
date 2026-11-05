using Devoctomy.Blazwiz.Sync.Configuration;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The sync profiles of this session: those configured when the app starts (<see cref="SyncOptions.Profiles"/>) and
/// those added while it runs (e.g. protected profiles the user creates). Each profile's stores are created here, once,
/// so the sync service, scheduler, storage and app code all see the same ones.
/// </summary>
public interface ISyncProfileRegistry
{
    /// <summary>
    /// Raised after a profile is added, with its name.
    /// </summary>
    event EventHandler<string>? ProfileAdded;

    /// <summary>
    /// Raised after a profile is removed, with its name.
    /// </summary>
    event EventHandler<string>? ProfileRemoved;

    /// <summary>
    /// Gets the profiles: the configured ones, in the order they were configured, then those added while the app
    /// runs, in the order they were added.
    /// </summary>
    IReadOnlyList<SyncProfileOptions> Profiles { get; }

    /// <summary>
    /// Gets a profile's configuration.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The configuration, or null when there is no such profile.</returns>
    SyncProfileOptions? Find(string name);

    /// <summary>
    /// Adds a profile while the app runs.
    /// </summary>
    /// <param name="profile">The profile's configuration. It needs a name and a remote pattern.</param>
    /// <exception cref="ArgumentException">A profile with that name already exists, or the configuration is incomplete.</exception>
    void Add(SyncProfileOptions profile);

    /// <summary>
    /// Removes a profile added while the app runs. Its files are left where they are.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>True when it was removed; false when there was no such profile.</returns>
    /// <exception cref="InvalidOperationException">The profile was configured when the app started.</exception>
    bool Remove(string name);

    /// <summary>
    /// Gets the profile's parts as the sync engine sees them.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="ArgumentException">There is no such profile.</exception>
    SyncProfile GetSyncProfile(string name);

    /// <summary>
    /// Gets the profile's file store for app code: writes and deletes are tracked for sync, and files are encrypted
    /// with the profile's own keys.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The file store.</returns>
    /// <exception cref="ArgumentException">There is no such profile.</exception>
    ISyncFileStore GetFileStore(string name);

    /// <summary>
    /// Gets the profile's file store without change tracking or encryption, as the sync engine uses it.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The file store.</returns>
    /// <exception cref="ArgumentException">There is no such profile.</exception>
    ISyncFileStore GetUntrackedFileStore(string name);

    /// <summary>
    /// Gets the profile's index store.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The index store.</returns>
    /// <exception cref="ArgumentException">There is no such profile.</exception>
    ISyncIndexStore GetIndexStore(string name);

    /// <summary>
    /// Gets the profile's remote pattern.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The matcher.</returns>
    /// <exception cref="ArgumentException">There is no such profile.</exception>
    ISyncPathMatcher GetMatcher(string name);
}
