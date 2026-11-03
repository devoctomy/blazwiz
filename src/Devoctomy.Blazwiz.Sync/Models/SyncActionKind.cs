namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// What a sync pass does with one file, decided by the planner.
/// </summary>
public enum SyncActionKind
{
    /// <summary>
    /// Both sides are unchanged.
    /// </summary>
    None,

    /// <summary>
    /// The local file changed: upload it, if the remote still has the base ETag.
    /// </summary>
    Upload,

    /// <summary>
    /// A new local file: upload it, if nothing exists remotely yet.
    /// </summary>
    UploadNew,

    /// <summary>
    /// The remote file changed (or the local copy is missing): download it.
    /// </summary>
    Download,

    /// <summary>
    /// A new remote file: download it.
    /// </summary>
    DownloadNew,

    /// <summary>
    /// The file exists on both sides with no base: compare the content, adopting it if equal.
    /// </summary>
    CompareContent,

    /// <summary>
    /// Deleted locally: delete it remotely, if the remote still has the base ETag.
    /// </summary>
    DeleteRemote,

    /// <summary>
    /// Deleted remotely and unchanged locally: delete the local copy.
    /// </summary>
    DeleteLocal,

    /// <summary>
    /// Gone from both sides: drop the index record.
    /// </summary>
    DropRecord,

    /// <summary>
    /// Changed on both sides: resolved by the conflict resolver.
    /// </summary>
    Conflict,
}
