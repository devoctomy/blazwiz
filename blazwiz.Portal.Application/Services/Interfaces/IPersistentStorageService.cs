namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Asks the browser to keep the site's storage after sign in, so unsynced changes are not cleared when space is short.
/// </summary>
public interface IPersistentStorageService
{
    /// <summary>
    /// Asks for persistent storage straight after the first successful sign in. Later sign ins only ask again if the
    /// browser said no and the app has since been installed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RequestAfterSignInAsync();
}
