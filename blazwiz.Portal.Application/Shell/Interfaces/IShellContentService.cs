namespace blazwiz.Portal.Application.Shell.Interfaces;

/// <summary>
/// Lets modules ask the app shell to show the current page again from scratch, for example after access to the files
/// it shows was restored.
/// </summary>
public interface IShellContentService
{
    /// <summary>
    /// Raised when the page content should be shown again from scratch.
    /// </summary>
    event EventHandler? ReloadRequested;

    /// <summary>
    /// Asks the shell to show the page content again from scratch.
    /// </summary>
    void RequestReload();
}
