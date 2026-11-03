using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Folder;

/// <summary>
/// Keeps a profile's files in a folder the user picked on their computer, at the same paths as in S3 (e.g.
/// <c>notes/Work/Ideas.md</c>). Each user picks their own folder. Only the profile's files are seen (its remote
/// pattern), so anything else in the folder is never listed, moved or deleted.
/// </summary>
/// <remarks>
/// <para>Every call first checks the browser still allows access to the folder, and throws
/// <see cref="SyncStorageAccessException"/> if not: the user must reconnect it after a click.</para>
/// <para>The modified time the app wrote is kept beside each file in IndexedDB as a <see cref="FolderFileRecord"/>,
/// with the time the file system gave the file then. If the file system's time has since changed, the file was edited
/// outside the app, and that time is reported instead. The file is always written first and deleted first.</para>
/// </remarks>
public class FolderSyncFileStore(
    IFolderAccessClient folderClient,
    IIndexedDbClient indexedDbClient,
    ISyncUserContext userContext,
    TimeProvider timeProvider,
    ISyncPathMatcher matcher,
    IndexedDbStore metadataStore)
    : ISyncFileStore
{
    /// <summary>
    /// The index of metadata records by owner.
    /// </summary>
    public const string OwnerIndex = "owner";

    private readonly IFolderAccessClient folderClient = folderClient ?? throw new ArgumentNullException(nameof(folderClient));
    private readonly IIndexedDbClient indexedDbClient = indexedDbClient ?? throw new ArgumentNullException(nameof(indexedDbClient));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ISyncPathMatcher matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));

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
        var owner = await GetAccessibleOwnerAsync();
        var entries = await folderClient.ListAsync(owner, matcher.Prefix);
        var records = await indexedDbClient.GetAllByIndexAsync<FolderFileRecord>(MetadataStore, OwnerIndex, owner);
        var known = records.ToDictionary(r => r.Key, StringComparer.Ordinal);
        return entries
            .Where(e => matcher.IsMatch(e.Key))
            .Select(e => new SyncFileInfo
            {
                Key = e.Key,
                ModifiedAt = GetModifiedAt(known.GetValueOrDefault(e.Key), e.LastModified),
                Size = e.Size,
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var owner = await GetAccessibleOwnerAsync();
        var file = await folderClient.ReadAsync(owner, key);
        if (file == null)
        {
            return null;
        }

        var record = await indexedDbClient.GetAsync<FolderFileRecord>(MetadataStore, new object[] { owner, key });
        return new SyncFile
        {
            Key = key,
            Content = file.Content ?? [],
            ModifiedAt = GetModifiedAt(record, file.LastModified),
        };
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetAccessibleOwnerAsync();
        var written = await folderClient.WriteAsync(owner, key, content);
        await indexedDbClient.PutAsync(MetadataStore, new FolderFileRecord
        {
            Owner = owner,
            Key = key,
            ModifiedAt = modifiedAt ?? timeProvider.GetUtcNow(),
            FileLastModified = written,
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

        var owner = await GetAccessibleOwnerAsync();
        await folderClient.DeleteAsync(owner, key);
        await indexedDbClient.DeleteManyAsync(MetadataStore, [new object[] { owner, key }]);
    }

    private static DateTimeOffset GetModifiedAt(FolderFileRecord? record, long fileLastModified)
    {
        return record != null && record.FileLastModified == fileLastModified
            ? record.ModifiedAt
            : DateTimeOffset.FromUnixTimeMilliseconds(fileLastModified);
    }

    private async Task<string> GetAccessibleOwnerAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
        var status = await folderClient.GetStatusAsync(owner)
            ?? throw new SyncStorageAccessException("No folder has been chosen for your files. Choose one in Settings.");
        return status.IsGranted
            ? owner
            : throw new SyncStorageAccessException($"Allow access to the folder '{status.Name}' to use your files.");
    }
}
