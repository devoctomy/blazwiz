namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Decides when sync passes run: on sign in, after local changes (debounced), when the app regains focus and when it
/// comes back online. Only one pass per profile runs at a time, across tabs too.
/// </summary>
public interface ISyncScheduler
{
    /// <summary>
    /// Gets a value indicating whether the scheduler has been started.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Starts scheduling (on sign in, or app start with a saved session): runs a full verify pass of every profile
    /// straight away and hooks the online and focus events. Does nothing if already started.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops scheduling (on sign out): cancels any pass and unhooks the events.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Asks for a pass of a profile after the change debounce. Requests during a pass run it again afterwards.
    /// </summary>
    /// <param name="profile">The profile.</param>
    void RequestSync(string profile);

    /// <summary>
    /// Runs a pass of every profile now (the Sync now button), if online.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SyncNowAsync();
}
