namespace Devoctomy.Blazwiz.UI.Shell.Interfaces;

/// <summary>
/// Says which component each of the framework's pages shows, or that it is turned off.
/// </summary>
public interface IPageRegistry
{
    /// <summary>
    /// Gets the component a page shows.
    /// </summary>
    /// <param name="key">The page, one of <see cref="BlazwizPages"/>.</param>
    /// <returns>The component type, or null when the page is turned off or was never added.</returns>
    Type? GetComponent(string key);

    /// <summary>
    /// Gets a value indicating whether a page is shown.
    /// </summary>
    /// <param name="key">The page, one of <see cref="BlazwizPages"/>.</param>
    /// <returns>True when the page has a component.</returns>
    bool IsEnabled(string key);
}
