using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Opfs;

/// <summary>
/// Keeps a profile's files in the origin private file system (OPFS), under <c>/&lt;root&gt;/&lt;owner&gt;/</c>, one
/// directory per key segment. Content is stored exactly as it is (bytes, any file type).
/// </summary>
/// <remarks>
/// OPFS cannot set a file's modified time, so the time to report is kept beside it in IndexedDB as an
/// <see cref="OpfsFileRecord"/>. OPFS is the source of truth for which files exist: a file without a record (a write
/// cut short) reports the time the browser wrote it. The file is always written first and deleted first.
/// </remarks>
public class OpfsSyncFileStore(
    IOpfsClient opfsClient,
    IIndexedDbClient indexedDbClient,
    ISyncUserContext userContext,
    TimeProvider timeProvider,
    string root,
    IndexedDbStore metadataStore)
    : ISyncFileStore
{
    /// <summary>
    /// The index of metadata records by owner.
    /// </summary>
    public const string OwnerIndex = "owner";

    private readonly IOpfsClient opfsClient = opfsClient ?? throw new ArgumentNullException(nameof(opfsClient));
    private readonly IIndexedDbClient indexedDbClient = indexedDbClient ?? throw new ArgumentNullException(nameof(indexedDbClient));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the OPFS directory the files are kept under.
    /// </summary>
    public string Root { get; } = string.IsNullOrWhiteSpace(root) ? throw new ArgumentException("A root directory is required.", nameof(root)) : root;

    /// <summary>
    /// Gets the object store the modified times are kept in.
    /// </summary>
    public IndexedDbStore MetadataStore { get; } = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));

    /// <summary>
    /// Describes the metadata object store for a database and store name: keyed by owner and key, indexed by owner.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The store name.</param>
    /// <returns>The store description.</returns>
    public static IndexedDbStore DescribeMetadataStore(string database, string name) =>
        new IndexedDbStore(database, name, ["owner", "key"], [new IndexedDbIndex(OwnerIndex, ["owner"])]);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        var entries = await opfsClient.ListAsync(Root, owner);
        var records = await indexedDbClient.GetAllByIndexAsync<OpfsFileRecord>(MetadataStore, OwnerIndex, owner);
        var modified = records.ToDictionary(r => r.Key, r => r.ModifiedAt, StringComparer.Ordinal);
        return entries
            .Select(e => new SyncFileInfo
            {
                Key = e.Key,
                ModifiedAt = modified.TryGetValue(e.Key, out var at) ? at : DateTimeOffset.FromUnixTimeMilliseconds(e.LastModified),
                Size = e.Size,
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var owner = await GetOwnerAsync();
        var file = await opfsClient.ReadAsync(Root, owner, key);
        if (file == null)
        {
            return null;
        }

        var record = await indexedDbClient.GetAsync<OpfsFileRecord>(MetadataStore, new object[] { owner, key });
        return new SyncFile
        {
            Key = key,
            Content = file.Content ?? [],
            ModifiedAt = record?.ModifiedAt ?? DateTimeOffset.FromUnixTimeMilliseconds(file.LastModified),
        };
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        await opfsClient.WriteAsync(Root, owner, key, content);
        await indexedDbClient.PutAsync(MetadataStore, new OpfsFileRecord
        {
            Owner = owner,
            Key = key,
            ModifiedAt = modifiedAt ?? timeProvider.GetUtcNow(),
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

        return WriteAsync(key, [], null, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var owner = await GetOwnerAsync();
        await opfsClient.DeleteAsync(Root, owner, key);
        await indexedDbClient.DeleteManyAsync(MetadataStore, [new object[] { owner, key }]);
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
    }
}
