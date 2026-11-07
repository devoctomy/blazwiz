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
    /// Gets or sets the key of the profile's display name in <see cref="DisplayTextResource"/>, or null to show its
    /// name.
    /// </summary>
    public string? DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the class the display name's resources are named after (e.g. the app's own resources).
    /// </summary>
    public Type? DisplayTextResource { get; set; }

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

    /// <summary>
    /// Gets the fixed start of <see cref="RemotePattern"/>: the folders before its first wildcard. Two profiles whose
    /// starts are the same, or one inside the other, could sync the same files.
    /// </summary>
    internal string RemotePrefix
    {
        get
        {
            var wildcard = RemotePattern.IndexOfAny(['*', '?', '[', '{']);
            var fixedPart = wildcard < 0 ? RemotePattern : RemotePattern[..wildcard];
            var end = fixedPart.LastIndexOf('/') + 1;
            return fixedPart[..end];
        }
    }

    /// <summary>
    /// Gets where on the device the profile keeps its index, files and their metadata, as far as they are known: a
    /// custom store (<see cref="IndexStoreFactory"/>, <see cref="FileStoreFactory"/>) is not.
    /// </summary>
    internal IEnumerable<string> DeviceStoreLocations
    {
        get
        {
            if (IndexStoreFactory == null)
            {
                yield return IndexedDbLocation(IndexDatabase, ResolvedIndexStoreName);
            }

            if (IsSwitchable)
            {
                foreach (var location in FileStores.SelectMany(s => s.Locations))
                {
                    yield return location;
                }
            }
            else if (FileStoreFactory == null)
            {
                yield return IndexedDbLocation(FileDatabase, ResolvedFileStoreName);
            }
        }
    }

    /// <summary>
    /// Names an IndexedDB object store, as <see cref="DeviceStoreLocations"/> lists it.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="store">The object store.</param>
    /// <returns>The name.</returns>
    internal static string IndexedDbLocation(string database, string store) => $"IndexedDB '{database}/{store}'";

    /// <summary>
    /// Names an OPFS directory, as <see cref="DeviceStoreLocations"/> lists it.
    /// </summary>
    /// <param name="root">The directory.</param>
    /// <returns>The name.</returns>
    internal static string OpfsLocation(string root) => $"OPFS '{root}'";

    /// <summary>
    /// Describes how this profile would clash with another, if it would: syncing the same remote files, or sharing a
    /// store on the device.
    /// </summary>
    /// <param name="other">The other profile.</param>
    /// <returns>Why they clash, or null if they do not.</returns>
    internal string? FindClash(SyncProfileOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Overlaps(other.RemotePrefix))
        {
            return $"the remote patterns '{RemotePattern}' and '{other.RemotePattern}' could match the same files";
        }

        var shared = DeviceStoreLocations.Intersect(other.DeviceStoreLocations, StringComparer.Ordinal).FirstOrDefault();
        return shared == null ? null : $"both keep data in {shared}";
    }

    /// <summary>
    /// Checks whether the profile's remote files could be under a folder, or the folder's files among the profile's.
    /// </summary>
    /// <param name="folderPrefix">The folder, ending in <c>/</c>, or empty for the user's whole root.</param>
    /// <returns>True if they could overlap.</returns>
    internal bool Overlaps(string folderPrefix)
    {
        var prefix = RemotePrefix;
        return prefix.StartsWith(folderPrefix, StringComparison.Ordinal) || folderPrefix.StartsWith(prefix, StringComparison.Ordinal);
    }
}
