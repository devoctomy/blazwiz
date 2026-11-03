namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// Which side's version of a file is kept after a conflict.
/// </summary>
public enum SyncConflictWinner
{
    /// <summary>
    /// The local version (or local delete) is kept, overwriting the remote.
    /// </summary>
    Local,

    /// <summary>
    /// The remote version is kept, overwriting the local.
    /// </summary>
    Remote,
}
