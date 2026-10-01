using System.Text.Json.Serialization;

namespace blazwiz.Core.Sync.Models;

/// <summary>
/// What a sync log entry records. Icons, labels and groups are mapped by the app.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SyncEventKind>))]
public enum SyncEventKind
{
    /// <summary>
    /// A pass started checking for changes.
    /// </summary>
    PassStarted,

    /// <summary>
    /// A pass finished. Consecutive passes that changed nothing are collapsed into one entry with a count.
    /// </summary>
    PassCompleted,

    /// <summary>
    /// A new local file was uploaded.
    /// </summary>
    UploadNew,

    /// <summary>
    /// A changed local file was uploaded.
    /// </summary>
    UploadLocalNewer,

    /// <summary>
    /// A new remote file was downloaded.
    /// </summary>
    DownloadNew,

    /// <summary>
    /// A changed remote file was downloaded.
    /// </summary>
    DownloadRemoteNewer,

    /// <summary>
    /// A file deleted locally was removed remotely.
    /// </summary>
    DeleteRemote,

    /// <summary>
    /// A file deleted remotely was removed locally.
    /// </summary>
    DeleteLocal,

    /// <summary>
    /// A file changed on both sides; the newer version was kept.
    /// </summary>
    ConflictResolved,

    /// <summary>
    /// A file was the same on both sides with no base, so nothing needed doing.
    /// </summary>
    AlreadyInSync,

    /// <summary>
    /// A file changed remotely during the pass (412), so it was checked again.
    /// </summary>
    Retried,

    /// <summary>
    /// An upload, download or delete failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The app went offline.
    /// </summary>
    WentOffline,

    /// <summary>
    /// The app came back online.
    /// </summary>
    WentOnline,

    /// <summary>
    /// Offline mode was turned on or off.
    /// </summary>
    OfflineModeChanged,
}
