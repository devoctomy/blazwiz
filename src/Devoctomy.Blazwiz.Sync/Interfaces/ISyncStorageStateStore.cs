using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Records which storage each user's profile uses on this device, and any move in progress.
/// </summary>
public interface ISyncStorageStateStore
{
    /// <summary>
    /// Gets a profile's state for a user.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="owner">The user id.</param>
    /// <returns>The state, or null if none has been recorded (the profile's default storage).</returns>
    Task<SyncStorageState?> GetAsync(string profile, string owner);

    /// <summary>
    /// Adds or replaces a profile's state for a user.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(SyncStorageState state);
}
