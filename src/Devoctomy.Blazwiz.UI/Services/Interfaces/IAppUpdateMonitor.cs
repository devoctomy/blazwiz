namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// Reports a new version of the app downloading in the background: the service worker installing while the current
/// version runs. The update is offered once the download has finished (the update banner); this is for the wait before
/// that, which on a slow connection is minutes.
/// </summary>
public interface IAppUpdateMonitor
{
    /// <summary>
    /// Raised when <see cref="IsDownloading"/> or <see cref="Progress"/> changes.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets a value indicating whether a new version is downloading.
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// Gets how much of the download is done, from 0 to 1, or null when the service worker does not report progress.
    /// </summary>
    double? Progress { get; }

    /// <summary>
    /// Starts watching the service worker. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();
}
