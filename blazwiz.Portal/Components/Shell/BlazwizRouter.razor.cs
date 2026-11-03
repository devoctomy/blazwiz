using System.Reflection;
using blazwiz.Portal.Layout;
using Microsoft.AspNetCore.Components;

namespace blazwiz.Portal.Components.Shell;

/// <summary>
/// The app's router. It finds the app's own pages and the framework's pages, wherever they are.
/// </summary>
public partial class BlazwizRouter : ComponentBase
{
    private IEnumerable<Assembly> additionalAssemblies = [];

    /// <summary>
    /// Gets or sets the assembly holding the app's own pages.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Assembly AppAssembly { get; set; } = default!;

    /// <summary>
    /// Gets or sets the layout for pages that do not choose one. Defaults to the main layout.
    /// </summary>
    [Parameter]
    public Type DefaultLayout { get; set; } = typeof(MainLayout);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        // The framework's pages, unless they are in the app's own assembly (the router rejects duplicates).
        additionalAssemblies = new[] { typeof(BlazwizRouter).Assembly }
            .Where(a => a != AppAssembly)
            .ToList();
    }
}
