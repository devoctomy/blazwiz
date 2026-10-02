namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Provides navigation between pages.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the specified URI.
    /// </summary>
    /// <param name="uri">The target URI.</param>
    void NavigateTo(string uri);

    /// <summary>
    /// Reloads the current page from the server, restarting the app.
    /// </summary>
    void Reload();
}
