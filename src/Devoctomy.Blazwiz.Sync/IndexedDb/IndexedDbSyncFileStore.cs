using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.IndexedDb;

/// <summary>
/// Keeps a profile's files in an IndexedDB object store, as <see cref="IndexedDbFileRecord"/>s keyed by owner and key.
/// Content is stored exactly as it is (bytes, any file type).
/// </summary>
public class IndexedDbSyncFileStore(IIndexedDbClient client, ISyncUserContext userContext, TimeProvider timeProvider, IndexedDbStore store)
    : ISyncFileStore
{
    /// <summary>
    /// The index of records by owner.
    /// </summary>
    public const string OwnerIndex = "owner";

    private static readonly IReadOnlyList<string> ListOmitFields = ["content"];

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the object store the files are kept in.
    /// </summary>
    public IndexedDbStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Describes the object store for a database and store name: keyed by owner and key, indexed by owner.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The store name.</param>
    /// <returns>The store description.</returns>
    public static IndexedDbStore DescribeStore(string database, string name) =>
        new IndexedDbStore(database, name, ["owner", "key"], [new IndexedDbIndex(OwnerIndex, ["owner"])]);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        var records = await client.GetAllByIndexAsync<IndexedDbFileRecord>(Store, OwnerIndex, owner, ListOmitFields);
        return records
            .Select(r => new SyncFileInfo { Key = r.Key, ModifiedAt = r.ModifiedAt, Size = r.Size })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var owner = await GetOwnerAsync();
        var record = await client.GetAsync<IndexedDbFileRecord>(Store, new object[] { owner, key });
        return record == null
            ? null
            : new SyncFile { Key = record.Key, Content = record.Content ?? [], ModifiedAt = record.ModifiedAt };
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        await client.PutAsync(Store, new IndexedDbFileRecord
        {
            Owner = owner,
            Key = key,
            Content = content,
            ModifiedAt = modifiedAt ?? timeProvider.GetUtcNow(),
            Size = content.Length,
        });
    }

    /// <inheritdoc/>
    public Task CreateFolderAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!key.EndsWith('/'))
        {
            throw new ArgumentException("Folder keys end with '/'.", nameof(key));
        }

        return WriteAsync(key, [], ct: ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var owner = await GetOwnerAsync();
        await client.DeleteManyAsync(Store, [new object[] { owner, key }]);
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
    }
}
