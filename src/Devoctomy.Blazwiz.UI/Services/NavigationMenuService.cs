using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// The sidebar navigation: every item added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddNavigationItem"/>, in order, without those linking to a
/// page that is turned off. Items with a <see cref="NavigationItem.TextResource"/> get their text from it.
/// </summary>
public class NavigationMenuService(IEnumerable<NavigationItem> items, IPageRegistry pageRegistry, IStringLocalizerFactory localizerFactory)
    : INavigationMenuService
{
    private readonly IReadOnlyList<NavigationItem> items = (items ?? throw new ArgumentNullException(nameof(items))).ToList();
    private readonly IPageRegistry pageRegistry = pageRegistry ?? throw new ArgumentNullException(nameof(pageRegistry));
    private readonly IStringLocalizerFactory localizerFactory = localizerFactory ?? throw new ArgumentNullException(nameof(localizerFactory));

    /// <inheritdoc/>
    public IReadOnlyList<NavigationItem> GetMainItems()
    {
        return items
            .Where(i => i.PageKey == null || pageRegistry.IsEnabled(i.PageKey))
            .OrderBy(i => i.Order)
            .Select(Localize)
            .ToList();
    }

    // Read on every call, so the current culture and the app's overrides apply.
    private NavigationItem Localize(NavigationItem item)
    {
        if (item.TextResource == null)
        {
            return item;
        }

        var text = localizerFactory.Create(item.TextResource);
        return new NavigationItem
        {
            Text = text[item.Text].Value,
            Description = string.IsNullOrEmpty(item.Description) ? string.Empty : text[item.Description].Value,
            Href = item.Href,
            Icon = item.Icon,
            ExactMatch = item.ExactMatch,
            Order = item.Order,
            PageKey = item.PageKey,
        };
    }
}
