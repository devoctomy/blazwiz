using blazwiz.Core.Sync.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.Sync.Configuration;

/// <summary>
/// Configures one profile's index store, file store and remote pattern. Each is set independently.
/// </summary>
/// <param name="options">The profile options to configure.</param>
public class SyncProfileBuilder(SyncProfileOptions options)
{
    /// <summary>
    /// Gets the profile options being configured.
    /// </summary>
    public SyncProfileOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Keeps the index in IndexedDB.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="store">The object store name.</param>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseIndexedDbIndexStore(string database, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        Options.IndexDatabase = database;
        Options.IndexStoreName = store;
        Options.IndexStoreFactory = null;
        return this;
    }

    /// <summary>
    /// Lets the user end to end encrypt the files: app code is given a store that encrypts on write and decrypts on
    /// read once encryption is on, and the files are kept and synced as ciphertext. Needs <c>AddBlazwizE2eEncryption</c>.
    /// </summary>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseEncryption()
    {
        Options.IsEncrypted = true;
        return this;
    }

    /// <summary>
    /// Keeps the index in a custom store, created from the service provider.
    /// </summary>
    /// <typeparam name="T">The store type.</typeparam>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseIndexStore<T>()
        where T : class, ISyncIndexStore
    {
        Options.IndexStoreFactory = sp => ActivatorUtilities.CreateInstance<T>(sp);
        return this;
    }

    /// <summary>
    /// Keeps the files in IndexedDB.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="store">The object store name.</param>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseIndexedDbFileStore(string database, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        Options.FileDatabase = database;
        Options.FileStoreName = store;
        Options.FileStoreFactory = null;
        ClearFileStores();
        return this;
    }

    /// <summary>
    /// Keeps the files in a custom store, created from the service provider.
    /// </summary>
    /// <typeparam name="T">The store type.</typeparam>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseFileStore<T>()
        where T : class, ISyncFileStore
    {
        Options.FileStoreFactory = sp => ActivatorUtilities.CreateInstance<T>(sp);
        ClearFileStores();
        return this;
    }

    /// <summary>
    /// Keeps the files in storages the user can switch between on each device (e.g. IndexedDB and OPFS). The files
    /// are moved with <see cref="ISyncStorageMigrationService"/>; the index stays where it is.
    /// </summary>
    /// <param name="configure">Adds the storages, and optionally sets the default. At least two are required.</param>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseFileStores(Action<SyncFileStoresBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ClearFileStores();
        configure(new SyncFileStoresBuilder(Options));
        if (Options.FileStores.Count < 2)
        {
            throw new ArgumentException($"Sync profile '{Options.Name}' needs at least two storages to switch between.", nameof(configure));
        }

        if (Options.DefaultStorageKind is { } kind && Options.FileStores.All(s => s.Kind != kind))
        {
            throw new ArgumentException($"The default storage of sync profile '{Options.Name}' ({kind}) has not been added.", nameof(configure));
        }

        Options.FileStoreFactory = null;
        return this;
    }

    /// <summary>
    /// Sets the files to sync: a glob relative to the user's root (<c>&lt;identity id&gt;/</c>), e.g. <c>notes/**/*.md</c>.
    /// <c>*</c> does not cross <c>/</c>, <c>**</c> does.
    /// </summary>
    /// <param name="pattern">The glob.</param>
    /// <param name="includeEmptyFolders">True to sync empty folders (folder markers) too.</param>
    /// <returns>The builder.</returns>
    public SyncProfileBuilder UseRemote(string pattern, bool includeEmptyFolders = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        Options.RemotePattern = pattern.TrimStart('/');
        Options.IncludeEmptyFolders = includeEmptyFolders;
        return this;
    }

    private void ClearFileStores()
    {
        Options.FileStores.Clear();
        Options.DefaultStorageKind = null;
    }
}
