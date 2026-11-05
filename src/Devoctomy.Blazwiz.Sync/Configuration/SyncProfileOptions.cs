using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// The configuration of one profile, built by <see cref="SyncProfileBuilder"/>.
/// </summary>
public class SyncProfileOptions
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the glob, relative to the user's root, that selects the profile's files, e.g. <c>documents/**/*.md</c>.
    /// </summary>
    public string RemotePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether empty folders (folder markers) are synced.
    /// </summary>
    public bool IncludeEmptyFolders { get; set; }

    /// <summary>
    /// Gets or sets the IndexedDB database holding the index, when <see cref="IndexStoreFactory"/> is not set.
    /// </summary>
    public string IndexDatabase { get; set; } = SyncDefaults.IndexDatabase;

    /// <summary>
    /// Gets or sets the IndexedDB object store holding the index, when <see cref="IndexStoreFactory"/> is not set.
    /// </summary>
    public string? IndexStoreName { get; set; }

    /// <summary>
    /// Gets or sets the IndexedDB database holding the files, when <see cref="FileStoreFactory"/> is not set.
    /// </summary>
    public string FileDatabase { get; set; } = SyncDefaults.FileDatabase;

    /// <summary>
    /// Gets or sets the IndexedDB object store holding the files, when <see cref="FileStoreFactory"/> is not set.
    /// </summary>
    public string? FileStoreName { get; set; }

    /// <summary>
    /// Gets or sets a factory for a custom index store, or null for IndexedDB.
    /// </summary>
    public Func<IServiceProvider, ISyncIndexStore>? IndexStoreFactory { get; set; }

    /// <summary>
    /// Gets or sets a factory for a custom file store, or null for IndexedDB.
    /// </summary>
    public Func<IServiceProvider, ISyncFileStore>? FileStoreFactory { get; set; }

    /// <summary>
    /// Gets the storages the user can switch between (<c>UseFileStores</c>), in the order they were added. Empty unless
    /// the profile can switch; <see cref="FileStoreFactory"/> and <see cref="FileDatabase"/> are then not used.
    /// </summary>
    public IList<SyncStorageOptions> FileStores { get; } = [];

    /// <summary>
    /// Gets or sets the storage used until the user picks another, or null for the first in <see cref="FileStores"/>.
    /// </summary>
    public SyncStorageKind? DefaultStorageKind { get; set; }

    /// <summary>
    /// Gets or sets the profile's key scope: its own keys and passphrase. Every profile has one: set with
    /// <c>UseKeyId</c> for a profile set up in code, and a new GUID for a profile a user creates.
    /// </summary>
    public E2eKeyScope? KeyScope { get; set; }

    /// <summary>
    /// Gets or sets the profile's security settings (idle timeout, device unlock, passphrase rules, Argon2id costs).
    /// </summary>
    public E2eProfileSettings Settings { get; set; } = new E2eProfileSettings();

    /// <summary>
    /// Gets a value indicating whether the user can switch the storage the files are kept in.
    /// </summary>
    public bool IsSwitchable => FileStores.Count > 0;

    /// <summary>
    /// Gets the storage used until the user picks another, when <see cref="IsSwitchable"/>.
    /// </summary>
    public SyncStorageKind ResolvedDefaultStorageKind => DefaultStorageKind ?? FileStores[0].Kind;

    /// <summary>
    /// Gets the IndexedDB object store holding the index: the configured one, or <c>&lt;profile&gt;_index</c>.
    /// </summary>
    public string ResolvedIndexStoreName => IndexStoreName ?? $"{Name}_index";

    /// <summary>
    /// Gets the IndexedDB object store holding the files: the configured one, or the profile name.
    /// </summary>
    public string ResolvedFileStoreName => FileStoreName ?? Name;
}
