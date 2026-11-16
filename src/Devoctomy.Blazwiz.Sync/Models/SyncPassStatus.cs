namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A pass, or other tracked work on a profile's synced files, that is running now: which profile, and how far it has
/// got. Each one has its own, so passes on different profiles running at the same time are shown apart.
/// </summary>
/// <param name="profile">The profile's name.</param>
/// <param name="done">How many of its changes are done.</param>
/// <param name="total">How many changes it planned, or 0 while it is still working out what to change.</param>
public class SyncPassStatus(string profile, int done, int total)
{
    /// <summary>
    /// Gets the profile's name.
    /// </summary>
    public string Profile { get; } = profile ?? throw new ArgumentNullException(nameof(profile));

    /// <summary>
    /// Gets how many of its changes are done.
    /// </summary>
    public int Done { get; } = done;

    /// <summary>
    /// Gets how many changes it planned, or 0 while it is still working out what to change.
    /// </summary>
    public int Total { get; } = total;
}
