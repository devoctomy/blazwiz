namespace blazwiz.Core.Sync.Configuration;

/// <summary>
/// Default names and service key parts used by the sync configuration. Names kept in the browser are short names: the
/// storage clients put the app's prefix in front of them (<see cref="Core.Configuration.Interfaces.IAppStorageKeys"/>).
/// </summary>
public static class SyncDefaults
{
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
}
