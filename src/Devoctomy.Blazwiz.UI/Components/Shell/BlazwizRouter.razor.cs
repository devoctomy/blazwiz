using System.Reflection;
using Devoctomy.Blazwiz.UI.Layout;
using Devoctomy.Blazwiz.UI.Shell;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Components.Shell;

/// <summary>
/// The app's router. It finds the app's own pages, the framework's, and those of the modules the app adds
/// (<see cref="Extensions.ShellServiceCollectionExtensions.AddPageAssembly"/>).
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

    [Inject]
    private IEnumerable<PageAssemblyRegistration> PageAssemblies { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        // The framework's pages and the modules', each once and not the app's own assembly (the router rejects
        // duplicates).
        additionalAssemblies = PageAssemblies
            .Select(p => p.Assembly)
            .Prepend(typeof(BlazwizRouter).Assembly)
            .Where(a => a != AppAssembly)
            .Distinct()
            .ToList();
    }
}
