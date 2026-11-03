namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// Per user preferences persisted in browser local storage.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Gets or sets the selected theme (see <see cref="ThemeNames"/>). When null the operating system preference is used.
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the navigation sidebar is collapsed to icons only.
    /// </summary>
    public bool SidebarCollapsed { get; set; }
}
