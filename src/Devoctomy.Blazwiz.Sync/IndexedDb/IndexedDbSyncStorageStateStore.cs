using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.IndexedDb;

/// <summary>
/// Keeps each user's <see cref="SyncStorageState"/> per profile in an IndexedDB object store, keyed by profile and
/// owner. It is read on every file operation, not cached, so a move made in another tab is seen straight away.
/// </summary>
public class IndexedDbSyncStorageStateStore(IIndexedDbClient client, IndexedDbStore store)
    : ISyncStorageStateStore
{
    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// Gets the object store the states are kept in.
    /// </summary>
    public IndexedDbStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Describes the object store for a database and store name: keyed by profile and owner.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The store name.</param>
    /// <returns>The store description.</returns>
    public static IndexedDbStore DescribeStore(string database, string name) =>
        new IndexedDbStore(database, name, ["profile", "owner"]);

    /// <inheritdoc/>
    public Task<SyncStorageState?> GetAsync(string profile, string owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile);
        ArgumentException.ThrowIfNullOrEmpty(owner);

        return client.GetAsync<SyncStorageState>(Store, new object[] { profile, owner });
    }

    /// <inheritdoc/>
    public Task SaveAsync(SyncStorageState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return client.PutAsync(Store, state);
    }
}
