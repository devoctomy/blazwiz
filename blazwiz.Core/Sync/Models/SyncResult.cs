namespace blazwiz.Core.Sync.Models;

/// <summary>
/// The outcome of a sync pass.
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Gets or sets the profile synced, or null for a pass over every profile.
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets the number of files uploaded.
    /// </summary>
    public int Uploaded { get; set; }

    /// <summary>
    /// Gets or sets the number of files downloaded.
    /// </summary>
    public int Downloaded { get; set; }

    /// <summary>
    /// Gets or sets the number of files deleted remotely.
    /// </summary>
    public int DeletedRemote { get; set; }

    /// <summary>
    /// Gets or sets the number of files deleted locally.
    /// </summary>
    public int DeletedLocal { get; set; }

    /// <summary>
    /// Gets or sets the number of files found to be the same on both sides with no base.
    /// </summary>
    public int Adopted { get; set; }

    /// <summary>
    /// Gets or sets the number of conflicts resolved.
    /// </summary>
    public int ConflictsResolved { get; set; }

    /// <summary>
    /// Gets or sets the number of files that failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets the errors, one per failed file (or one for the whole pass).
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets the keys of local files the pass changed (downloaded or deleted), by profile.
    /// </summary>
    public Dictionary<string, List<string>> ChangedLocalKeys { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the pass changed anything on either side.
    /// </summary>
    public bool HasChanges => Uploaded + Downloaded + DeletedRemote + DeletedLocal + ConflictsResolved > 0;

    /// <summary>
    /// Records a local file changed by the pass.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="key">The key.</param>
    public void AddChangedLocalKey(string profile, string key)
    {
        if (!ChangedLocalKeys.TryGetValue(profile, out var keys))
        {
            keys = [];
            ChangedLocalKeys[profile] = keys;
        }

        keys.Add(key);
    }

    /// <summary>
    /// Adds the counts, errors and changed keys of another result to this one.
    /// </summary>
    /// <param name="other">The other result.</param>
    public void Add(SyncResult other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Uploaded += other.Uploaded;
        Downloaded += other.Downloaded;
        DeletedRemote += other.DeletedRemote;
        DeletedLocal += other.DeletedLocal;
        Adopted += other.Adopted;
        ConflictsResolved += other.ConflictsResolved;
        Failed += other.Failed;
        Errors.AddRange(other.Errors);
        foreach (var (profile, keys) in other.ChangedLocalKeys)
        {
            foreach (var key in keys)
            {
                AddChangedLocalKey(profile, key);
            }
        }
    }
}
