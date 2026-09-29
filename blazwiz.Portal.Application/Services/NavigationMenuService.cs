using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Navigation;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Defines the sidebar navigation. To add a new page to the app, create the page and add an entry here.
/// </summary>
public class NavigationMenuService : INavigationMenuService
{
    private static readonly IReadOnlyList<NavigationItem> MainItems =
    [
        new NavigationItem
        {
            Text = "Dashboard",
            Href = string.Empty,
            Icon = "bi-grid-1x2",
            ExactMatch = true,
            Description = "Your home page.",
        },
        new NavigationItem
        {
            Text = "Showcase",
            Href = "showcase",
            Icon = "bi-palette",
            Description = "Themed building blocks to copy into new pages.",
        },
        new NavigationItem
        {
            Text = "Settings",
            Href = "settings",
            Icon = "bi-gear",
            Description = "Appearance and application preferences.",
        },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<NavigationItem> GetMainItems() => MainItems;
}
