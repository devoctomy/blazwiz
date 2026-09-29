using blazwiz.Portal.Models.Navigation;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Supplies the items shown in the navigation sidebar.
/// </summary>
public interface INavigationMenuService
{
    /// <summary>
    /// Gets the main navigation items, in display order.
    /// </summary>
    /// <returns>The navigation items.</returns>
    IReadOnlyList<NavigationItem> GetMainItems();
}
