namespace Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

/// <summary>
/// Checks the session with the sign-in service whenever the app resumes (its page becomes visible again or the
/// browser comes back online), so a device that was closed, offline or asleep when the user signed out everywhere
/// signs itself out as soon as it wakes. Started and stopped with the session by <see cref="SessionResumeSessionHandler"/>.
/// </summary>
public interface ISessionResumeMonitor
{
    /// <summary>
    /// Raised after a resume once the check found the session still good, so modules can reconnect what they hold.
    /// When the check refuses the session, it ends instead (<c>SessionEnded</c>, reason <c>Revoked</c>).
    /// </summary>
    event EventHandler? SessionResumed;

    /// <summary>
    /// Starts watching for the app resuming. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops watching.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Checks the session now, and raises <see cref="SessionResumed"/> if it is still good. Called when the app
    /// resumes; a check already running covers a second call.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CheckAsync();
}
