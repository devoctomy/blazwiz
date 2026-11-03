using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Navigation;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// The sidebar navigation: every item added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddNavigationItem"/>, in order, without those linking to a
/// page that is turned off.
/// </summary>
public class NavigationMenuService(IEnumerable<NavigationItem> items, IPageRegistry pageRegistry)
    : INavigationMenuService
{
    private readonly IReadOnlyList<NavigationItem> items = (items ?? throw new ArgumentNullException(nameof(items))).ToList();
    private readonly IPageRegistry pageRegistry = pageRegistry ?? throw new ArgumentNullException(nameof(pageRegistry));

    /// <inheritdoc/>
    public IReadOnlyList<NavigationItem> GetMainItems()
    {
        return items
            .Where(i => i.PageKey == null || pageRegistry.IsEnabled(i.PageKey))
            .OrderBy(i => i.Order)
            .ToList();
    }
}
