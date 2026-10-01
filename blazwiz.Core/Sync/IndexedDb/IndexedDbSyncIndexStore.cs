using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.IndexedDb;

/// <summary>
/// Keeps a profile's sync index in an IndexedDB object store, keyed by profile, owner and key, so several profiles
/// can share one store.
/// </summary>
public class IndexedDbSyncIndexStore(IIndexedDbClient client, IndexedDbStore store)
    : ISyncIndexStore
{
    /// <summary>
    /// The index of records by profile and owner.
    /// </summary>
    public const string ProfileOwnerIndex = "profile_owner";

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// Gets the object store the index is kept in.
    /// </summary>
    public IndexedDbStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Describes the object store for a database and store name: keyed by profile, owner and key.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The store name.</param>
    /// <returns>The store description.</returns>
    public static IndexedDbStore DescribeStore(string database, string name) =>
        new IndexedDbStore(database, name, ["profile", "owner", "key"], [new IndexedDbIndex(ProfileOwnerIndex, ["profile", "owner"])]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<SyncIndexRecord>> GetAllAsync(string profile, string owner, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile);
        ArgumentException.ThrowIfNullOrEmpty(owner);

        return client.GetAllByIndexAsync<SyncIndexRecord>(Store, ProfileOwnerIndex, new object[] { profile, owner });
    }

    /// <inheritdoc/>
    public Task<SyncIndexRecord?> GetAsync(string profile, string owner, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(key);

        return client.GetAsync<SyncIndexRecord>(Store, new object[] { profile, owner, key });
    }

    /// <inheritdoc/>
    public Task SaveAsync(SyncIndexRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        return client.PutAsync(Store, record);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string profile, string owner, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(key);

        return client.DeleteManyAsync(Store, [new object[] { profile, owner, key }]);
    }
}
