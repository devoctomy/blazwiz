namespace Devoctomy.Blazwiz.Sync.Exceptions;

/// <summary>
/// The codes of sync errors users can see. The UI shows each with the text resource <c>Error.&lt;code&gt;</c>.
/// </summary>
public static class SyncErrorCodes
{
    /// <summary>
    /// The files are being moved to another storage; try again in a moment.
    /// </summary>
    public const string StorageMigrating = "Sync.Storage.Migrating";

    /// <summary>
    /// A file could not be read while moving the files. Argument: the file's key.
    /// </summary>
    public const string StorageReadFailed = "Sync.Storage.ReadFailed";

    /// <summary>
    /// A file did not copy correctly while moving the files. Argument: the file's key.
    /// </summary>
    public const string StorageCopyMismatch = "Sync.Storage.CopyMismatch";

    /// <summary>
    /// The chosen folder already holds files. Argument: the profile's name.
    /// </summary>
    public const string StorageFolderNotEmpty = "Sync.Storage.FolderNotEmpty";

    /// <summary>
    /// No folder has been chosen for the files.
    /// </summary>
    public const string FolderNotChosen = "Sync.Folder.NotChosen";

    /// <summary>
    /// The browser needs permission for the folder again. Argument: the folder's name.
    /// </summary>
    public const string FolderAccessNeeded = "Sync.Folder.AccessNeeded";

    /// <summary>
    /// There are no credentials for the remote store.
    /// </summary>
    public const string RemoteNoCredentials = "Sync.Remote.NoCredentials";

    /// <summary>
    /// No cloud target is set up to sync with (e.g. local development without a data bucket).
    /// </summary>
    public const string RemoteNotConfigured = "Sync.Remote.NotConfigured";

    /// <summary>
    /// A remote file is larger than the app syncs (<c>SyncOptions.MaxFileSize</c>). Arguments: its size and the limit,
    /// in MB.
    /// </summary>
    public const string RemoteTooLarge = "Sync.Remote.TooLarge";

    /// <summary>
    /// The remote store could not be reached.
    /// </summary>
    public const string RemoteUnreachable = "Sync.Remote.Unreachable";

    /// <summary>
    /// The remote store refused the credentials.
    /// </summary>
    public const string RemoteAccessDenied = "Sync.Remote.AccessDenied";

    /// <summary>
    /// A file changed in the remote store since it was listed. Sync checks it again.
    /// </summary>
    public const string RemoteChanged = "Sync.Remote.Changed";

    /// <summary>
    /// Some files could not be synced. Argument: how many.
    /// </summary>
    public const string FilesFailed = "Sync.FilesFailed";
}
