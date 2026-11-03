namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Watches whether the browser still allows access to the folder the files are kept in (when the user chose a folder
/// on their computer), and restores it. Browsers can take the permission away, for example while a phone app is in
/// the background, and only ask again after a click.
/// </summary>
public interface ISyncFolderAccessMonitor
{
    /// <summary>
    /// Raised when <see cref="NeedsAccess"/>, <see cref="FolderName"/> or <see cref="Error"/> change.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets a value indicating whether access to the folder must be allowed again before the files can be used.
    /// </summary>
    bool NeedsAccess { get; }

    /// <summary>
    /// Gets the folder's name, when known.
    /// </summary>
    string? FolderName { get; }

    /// <summary>
    /// Gets why the last attempt to restore access failed, if it did.
    /// </summary>
    string? Error { get; }

    /// <summary>
    /// Starts watching: checks again whenever the app comes back to the foreground. Called when a session starts.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops watching. Called when the user signs out.
    /// </summary>
    void Stop();

    /// <summary>
    /// Checks whether access is needed, and updates <see cref="NeedsAccess"/>.
    /// </summary>
    /// <returns>True if access must be allowed again.</returns>
    Task<bool> CheckAsync();

    /// <summary>
    /// Asks the browser for access again (must follow a click). On success the page content is shown again and sync
    /// starts.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ReconnectAsync();
}
