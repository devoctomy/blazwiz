namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// Default names and service key parts used by the sync configuration. Names kept in the browser are short names: the
/// storage clients put the app's prefix in front of them (<see cref="Core.Configuration.Interfaces.IAppStorageKeys"/>).
/// </summary>
public static class SyncDefaults
{
    /// <summary>
    /// The configuration section read by <see cref="SyncBuilder.UseEmbeddedConfiguration"/>.
    /// </summary>
    public const string ConfigurationSection = "Sync";

    /// <summary>
    /// The largest remote file a pass downloads unless <c>UseMaxFileSize</c> says otherwise: 100 MiB. Files are held
    /// whole in memory while they are synced, so one much larger than the app expects could bring the tab down.
    /// </summary>
    public const long MaxFileSize = 100L * 1024 * 1024;

    /// <summary>
    /// The folder, in the user's prefix, holding a folder per protected profile, named by its id.
    /// </summary>
    public const string ProtectedProfilesFolder = "profiles";

    /// <summary>
    /// The name the storage chosen for the device is recorded under, among the profiles' storage states. Not a
    /// profile name: profiles cannot be called this.
    /// </summary>
    public const string DeviceDefaultStorageName = ":device";

    /// <summary>
    /// The IndexedDB database holding the sync index and the log, unless configured otherwise.
    /// </summary>
    public const string IndexDatabase = "sync";

    /// <summary>
    /// The IndexedDB database holding the files, unless configured otherwise.
    /// </summary>
    public const string FileDatabase = "files";

    /// <summary>
    /// The IndexedDB object store holding the log, unless configured otherwise.
    /// </summary>
    public const string EventLogStore = "events";

    /// <summary>
    /// Added to a profile name for the service key of its file store without change tracking, used by the engine.
    /// </summary>
    public const string UntrackedFileStoreKeySuffix = ":untracked";

    /// <summary>
    /// Put before a profile name for the cross tab lock taken while it syncs.
    /// </summary>
    public const string LockNamePrefix = "sync-";

    /// <summary>
    /// The IndexedDB object store (in <see cref="IndexDatabase"/>) recording which storage each profile uses.
    /// </summary>
    public const string StorageStateStore = "storage";

    /// <summary>
    /// Added to a profile name for the IndexedDB object store (in <see cref="IndexDatabase"/>) holding its OPFS files'
    /// modified times, unless configured otherwise.
    /// </summary>
    public const string OpfsMetadataStoreSuffix = "_opfs";

    /// <summary>
    /// Added to a profile name for the IndexedDB object store (in <see cref="IndexDatabase"/>) holding its local folder
    /// files' modified times, unless configured otherwise.
    /// </summary>
    public const string FolderMetadataStoreSuffix = "_folder";

    /// <summary>
    /// The BroadcastChannel the app's tabs use to tell each other about storage moves.
    /// </summary>
    public const string StorageChannelName = "sync-storage";

    /// <summary>
    /// The BroadcastChannel the app's tabs use to lock a protected profile in every tab.
    /// </summary>
    public const string LockChannelName = "e2e-lock";
}
