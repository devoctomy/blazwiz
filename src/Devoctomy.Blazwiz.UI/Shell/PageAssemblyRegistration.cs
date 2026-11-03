using System.Reflection;

namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// An assembly with routable pages (<c>@page</c>) for the router to find, e.g. a module's. Added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddPageAssembly"/>.
/// </summary>
public sealed class PageAssemblyRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageAssemblyRegistration"/> class.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    public PageAssemblyRegistration(Assembly assembly)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    /// <summary>
    /// Gets the assembly.
    /// </summary>
    public Assembly Assembly { get; }
}
