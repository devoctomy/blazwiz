namespace blazwiz.Portal.Models.Navigation;

/// <summary>
/// An entry in the navigation sidebar.
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Gets or sets the text shown when the sidebar is expanded, and as a tooltip when collapsed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative URL to navigate to, e.g. <c>settings</c>. Use an empty string for the root page.
    /// </summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Bootstrap Icons class, e.g. <c>bi-speedometer2</c>.
    /// </summary>
    public string Icon { get; set; } = "bi-circle";

    /// <summary>
    /// Gets or sets a value indicating whether the item is only active when the URL matches exactly.
    /// </summary>
    public bool ExactMatch { get; set; }

    /// <summary>
    /// Gets or sets a short description of the page, used for quick links on the dashboard.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item's position in the sidebar: lower comes first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the framework page the item links to, if any (see <c>BlazwizPages</c>). The item is hidden when that
    /// page is turned off.
    /// </summary>
    public string? PageKey { get; set; }
}
