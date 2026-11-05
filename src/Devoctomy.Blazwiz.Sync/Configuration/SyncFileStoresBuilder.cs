using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Opfs;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// Configures the storages a profile's files can be kept in, for the user to switch between. Used by
/// <see cref="SyncProfileBuilder.UseFileStores"/>.
/// </summary>
/// <param name="options">The profile options to configure.</param>
public class SyncFileStoresBuilder(SyncProfileOptions options)
{
    /// <summary>
    /// Gets the profile options being configured.
    /// </summary>
    public SyncProfileOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Adds IndexedDB as a storage.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="store">The object store name.</param>
    /// <returns>The builder.</returns>
    public SyncFileStoresBuilder AddIndexedDb(string database, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        var description = IndexedDbSyncFileStore.DescribeStore(database, store);
        return Add(SyncStorageKind.IndexedDb, sp => new IndexedDbSyncFileStore(
            sp.GetRequiredService<IIndexedDbClient>(),
            sp.GetRequiredService<ISyncUserContext>(),
            sp.GetRequiredService<TimeProvider>(),
            description));
    }

    /// <summary>
    /// Adds the origin private file system (OPFS) as a storage.
    /// </summary>
    /// <param name="root">The OPFS directory the files are kept under.</param>
    /// <param name="metadataDatabase">The IndexedDB database for the files' modified times.</param>
    /// <param name="metadataStore">The IndexedDB object store for the files' modified times, or null for <c>&lt;profile&gt;_opfs</c>.</param>
    /// <returns>The builder.</returns>
    public SyncFileStoresBuilder AddOpfs(string root, string metadataDatabase = SyncDefaults.IndexDatabase, string? metadataStore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataDatabase);

        var description = OpfsSyncFileStore.DescribeMetadataStore(metadataDatabase, metadataStore ?? Options.Name + SyncDefaults.OpfsMetadataStoreSuffix);
        return Add(SyncStorageKind.Opfs, sp => new OpfsSyncFileStore(
            sp.GetRequiredService<IOpfsClient>(),
            sp.GetRequiredService<IIndexedDbClient>(),
            sp.GetRequiredService<ISyncUserContext>(),
            sp.GetRequiredService<TimeProvider>(),
            root,
            description));
    }

    /// <summary>
    /// Adds a folder the user picks on their computer as a storage (File System Access API; Chromium desktop only).
    /// Files are kept at the same paths as in S3, and only those matching the profile's remote pattern are seen.
    /// </summary>
    /// <param name="metadataDatabase">The IndexedDB database for the files' modified times.</param>
    /// <param name="metadataStore">The IndexedDB object store for the files' modified times, or null for <c>&lt;profile&gt;_folder</c>.</param>
    /// <returns>The builder.</returns>
    public SyncFileStoresBuilder AddLocalFolder(string metadataDatabase = SyncDefaults.IndexDatabase, string? metadataStore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataDatabase);

        var name = Options.Name;
        var description = FolderSyncFileStore.DescribeMetadataStore(metadataDatabase, metadataStore ?? name + SyncDefaults.FolderMetadataStoreSuffix);
        return Add(SyncStorageKind.LocalFolder, sp => new FolderSyncFileStore(
            sp.GetRequiredService<IFolderAccessClient>(),
            sp.GetRequiredService<IIndexedDbClient>(),
            sp.GetRequiredService<ISyncUserContext>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ISyncProfileRegistry>().GetMatcher(name),
            description));
    }

    /// <summary>
    /// Sets the storage used until the user picks another. Without this, the first one added is used.
    /// </summary>
    /// <param name="kind">The storage. It must be added too.</param>
    /// <returns>The builder.</returns>
    public SyncFileStoresBuilder UseDefault(SyncStorageKind kind)
    {
        Options.DefaultStorageKind = kind;
        return this;
    }

    private SyncFileStoresBuilder Add(SyncStorageKind kind, Func<IServiceProvider, ISyncFileStore> factory)
    {
        if (Options.FileStores.Any(s => s.Kind == kind))
        {
            throw new ArgumentException($"The {kind} storage has already been added to sync profile '{Options.Name}'.", nameof(kind));
        }

        Options.FileStores.Add(new SyncStorageOptions(kind, factory));
        return this;
    }
}
