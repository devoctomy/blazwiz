namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The browser's view of the network (<c>navigator.onLine</c>) and the <c>online</c>, <c>offline</c> and
/// <c>visibilitychange</c> events, through <c>wwwroot/js/network.js</c>.
/// </summary>
public interface IBrowserNetworkStatus
{
    /// <summary>
    /// Raised when the browser goes online (true) or offline (false).
    /// </summary>
    event EventHandler<bool>? OnlineChanged;

    /// <summary>
    /// Raised when the app becomes visible again (it regained focus).
    /// </summary>
    event EventHandler? BecameVisible;

    /// <summary>
    /// Gets whether the browser reports a network. True does not mean S3 is reachable.
    /// </summary>
    /// <returns>True if the browser reports a network.</returns>
    Task<bool> IsOnlineAsync();

    /// <summary>
    /// Starts listening to the browser events. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops listening to the browser events.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();
}
