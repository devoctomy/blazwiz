namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Progress of a move of files between storages.
/// </summary>
/// <param name="profile">The profile being moved.</param>
/// <param name="done">The files copied so far.</param>
/// <param name="total">The files to copy.</param>
public class SyncStorageMigrationProgress(string profile, int done, int total)
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
}
