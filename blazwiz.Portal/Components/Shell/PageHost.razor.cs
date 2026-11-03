using blazwiz.Portal.Application.Shell.Interfaces;
using Microsoft.AspNetCore.Components;

namespace blazwiz.Portal.Components.Shell;

/// <summary>
/// Shows the component registered for one of the framework's pages, or Not Found when the page is turned off. Each
/// framework route is a small page that renders this, so the app can replace the page in dependency injection.
/// </summary>
public partial class PageHost : ComponentBase
{
    private Type? componentType;

    /// <summary>
    /// Gets or sets the page, one of <see cref="Application.Shell.BlazwizPages"/>.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Key { get; set; } = string.Empty;

    [Inject]
    private IPageRegistry PageRegistry { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        componentType = PageRegistry.GetComponent(Key);
    }
}
