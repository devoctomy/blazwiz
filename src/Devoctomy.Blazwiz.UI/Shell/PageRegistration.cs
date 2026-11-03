namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// The component shown for one of the framework's pages, or that the page is turned off. The last registration for a
/// page wins, so the app can replace or turn off a page the framework added.
/// </summary>
public sealed class PageRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageRegistration"/> class.
    /// </summary>
    /// <param name="key">The page, one of <see cref="BlazwizPages"/>.</param>
    /// <param name="componentType">The component shown on the page, or null when the page is turned off.</param>
    public PageRegistration(string key, Type? componentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        ComponentType = componentType;
    }

    /// <summary>
    /// Gets the page, one of <see cref="BlazwizPages"/>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the component shown on the page, or null when the page is turned off.
    /// </summary>
    public Type? ComponentType { get; }
}
