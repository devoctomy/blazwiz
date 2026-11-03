namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// What a <see cref="SyncEvent"/> says, as codes. The UI shows each with the text resource
/// <c>Sync.Log.&lt;code&gt;</c>, formatted with <see cref="SyncEvent.Arguments"/>, so the log can be reworded and
/// translated.
/// </summary>
public static class SyncEventCodes
{
    /// <summary>
    /// A pass started checking for changes.
    /// </summary>
    public const string Checking = "Checking";

    /// <summary>
    /// A pass found nothing to do.
    /// </summary>
    public const string NoChanges = "NoChanges";

    /// <summary>
    /// A pass finished. Arguments: uploaded, downloaded, deleted, conflicts resolved and failed counts.
    /// </summary>
    public const string Summary = "Summary";

    /// <summary>
    /// A pass was stopped, e.g. by signing out.
    /// </summary>
    public const string Stopped = "Stopped";

    /// <summary>
    /// A pass failed. Argument: the error's code (see <see cref="Exceptions.SyncErrorCodes"/>).
    /// </summary>
    public const string PassFailed = "PassFailed";

    /// <summary>
    /// A file could not be uploaded. Arguments: the error's code and its arguments, when known.
    /// </summary>
    public const string UploadFailed = "UploadFailed";

    /// <summary>
    /// A file could not be downloaded. Arguments: the error's code and its arguments, when known.
    /// </summary>
    public const string DownloadFailed = "DownloadFailed";

    /// <summary>
    /// A file could not be deleted. Arguments: the error's code and its arguments, when known.
    /// </summary>
    public const string DeleteFailed = "DeleteFailed";

    /// <summary>
    /// A file could not be synced. Arguments: the error's code and its arguments, when known.
    /// </summary>
    public const string FileFailed = "FileFailed";

    /// <summary>
    /// A file changed remotely during the pass and is checked again.
    /// </summary>
    public const string ChangedDuringSync = "ChangedDuringSync";

    /// <summary>
    /// A file is the same on both sides.
    /// </summary>
    public const string AlreadyInSync = "AlreadyInSync";

    /// <summary>
    /// A new local file is uploaded.
    /// </summary>
    public const string NewLocalFile = "NewLocalFile";

    /// <summary>
    /// A file changed locally is uploaded.
    /// </summary>
    public const string LocalNewer = "LocalNewer";

    /// <summary>
    /// A new remote file is downloaded.
    /// </summary>
    public const string NewRemoteFile = "NewRemoteFile";

    /// <summary>
    /// A file changed remotely is downloaded.
    /// </summary>
    public const string RemoteNewer = "RemoteNewer";

    /// <summary>
    /// A file deleted locally is deleted remotely.
    /// </summary>
    public const string DeletedLocally = "DeletedLocally";

    /// <summary>
    /// A file deleted remotely is deleted locally.
    /// </summary>
    public const string DeletedRemotely = "DeletedRemotely";

    /// <summary>
    /// A file changed on both sides; the local version was newer and was kept.
    /// </summary>
    public const string ConflictKeptLocal = "ConflictKeptLocal";

    /// <summary>
    /// A file changed on both sides; the remote version was newer and was kept.
    /// </summary>
    public const string ConflictKeptRemote = "ConflictKeptRemote";

    /// <summary>
    /// The connection came back.
    /// </summary>
    public const string BackOnline = "BackOnline";

    /// <summary>
    /// The app went offline. Argument: the <see cref="Connectivity.OfflineReason"/>.
    /// </summary>
    public const string WentOffline = "WentOffline";

    /// <summary>
    /// The user turned sync on.
    /// </summary>
    public const string SyncTurnedOn = "SyncTurnedOn";

    /// <summary>
    /// The user turned sync off.
    /// </summary>
    public const string SyncTurnedOff = "SyncTurnedOff";

    /// <summary>
    /// Offline mode was turned on.
    /// </summary>
    public const string OfflineModeOn = "OfflineModeOn";

    /// <summary>
    /// Offline mode was turned off.
    /// </summary>
    public const string OfflineModeOff = "OfflineModeOff";
}
