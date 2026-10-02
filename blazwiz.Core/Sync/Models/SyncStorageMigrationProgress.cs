namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Progress of a move of files between storages.
/// </summary>
/// <param name="profile">The profile being moved.</param>
/// <param name="done">The files copied so far.</param>
/// <param name="total">The files to copy.</param>
/// <param name="isSyncing">True while the profile is synced before the copy starts.</param>
public class SyncStorageMigrationProgress(string profile, int done, int total, bool isSyncing = false)
{
    /// <summary>
    /// Gets the profile being moved.
    /// </summary>
    public string Profile { get; } = profile;

    /// <summary>
    /// Gets the files copied so far.
    /// </summary>
    public int Done { get; } = done;

    /// <summary>
    /// Gets the files to copy.
    /// </summary>
    public int Total { get; } = total;

    /// <summary>
    /// Gets a value indicating whether the profile is being synced before the copy starts.
    /// </summary>
    public bool IsSyncing { get; } = isSyncing;
}
