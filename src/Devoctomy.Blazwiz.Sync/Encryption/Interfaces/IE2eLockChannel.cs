namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Tells the app's other tabs when a protected profile is locked, so it is locked in every tab, through
/// <c>wwwroot/js/channel.js</c>. Unlocking is never sent: each tab unlocks for itself.
/// </summary>
public interface IE2eLockChannel
{
    /// <summary>
    /// Raised when another tab locked a protected profile, with its id.
    /// </summary>
    event EventHandler<string>? LockReceived;

    /// <summary>
    /// Starts listening to other tabs. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Tells the other tabs a protected profile was locked.
    /// </summary>
    /// <param name="profileId">The profile's id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PostLockAsync(string profileId);
}
