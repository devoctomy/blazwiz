namespace blazwiz.Core.Sync.Configuration;

/// <summary>
/// Default names and service key parts used by the sync configuration.
/// </summary>
public static class SyncDefaults
{
    /// <summary>
    /// The IndexedDB database holding the sync index and the log, unless configured otherwise.
    /// </summary>
    public const string IndexDatabase = "blazwiz_sync";

    /// <summary>
    /// The IndexedDB database holding the files, unless configured otherwise.
    /// </summary>
    public const string FileDatabase = "blazwiz_files";

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
    public const string LockNamePrefix = "blazwiz-sync-";
}
