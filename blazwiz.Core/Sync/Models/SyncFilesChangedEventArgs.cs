namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Raised when a sync pass changes local files (downloads or remote deletes), so open views can refresh.
/// </summary>
/// <param name="profile">The profile.</param>
/// <param name="keys">The keys of the changed files.</param>
public class SyncFilesChangedEventArgs(string profile, IReadOnlyList<string> keys)
    : EventArgs
{
    /// <summary>
    /// Gets the profile.
    /// </summary>
    public string Profile { get; } = profile ?? throw new ArgumentNullException(nameof(profile));

    /// <summary>
    /// Gets the keys of the changed files.
    /// </summary>
    public IReadOnlyList<string> Keys { get; } = keys ?? throw new ArgumentNullException(nameof(keys));
}
