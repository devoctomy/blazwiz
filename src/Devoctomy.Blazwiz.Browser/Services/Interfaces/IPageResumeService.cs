namespace Devoctomy.Blazwiz.Browser.Services.Interfaces;

/// <summary>
/// Tells when the app resumes: its page becomes visible again (the user came back to the tab, or a mobile browser
/// woke a backgrounded app) or the browser comes back online. Mobile browsers freeze a backgrounded app and drop its
/// connections, so this is when it should check what changed while it slept.
/// </summary>
public interface IPageResumeService
{
    /// <summary>
    /// Raised when the page becomes visible again or the browser comes back online.
    /// </summary>
    event EventHandler? Resumed;

    /// <summary>
    /// Starts listening to the browser. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops listening to the browser.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();
}
