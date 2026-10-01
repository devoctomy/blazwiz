using blazwiz.Core.Sync.Interfaces;

namespace blazwiz.Core.Sync.Configuration;

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
    /// Gets or sets the glob, relative to the user's root, that selects the profile's files, e.g. <c>notes/**/*.md</c>.
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
    /// Gets the IndexedDB object store holding the index: the configured one, or <c>&lt;profile&gt;_index</c>.
    /// </summary>
    public string ResolvedIndexStoreName => IndexStoreName ?? $"{Name}_index";

    /// <summary>
    /// Gets the IndexedDB object store holding the files: the configured one, or the profile name.
    /// </summary>
    public string ResolvedFileStoreName => FileStoreName ?? Name;
}
