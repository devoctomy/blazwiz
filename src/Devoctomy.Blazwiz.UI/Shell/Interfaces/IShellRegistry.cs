namespace Devoctomy.Blazwiz.UI.Shell.Interfaces;

/// <summary>
/// Says which components each region of the app shell shows.
/// </summary>
public interface IShellRegistry
{
    /// <summary>
    /// Gets the components shown in a region, in order.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The component types.</returns>
    IReadOnlyList<Type> GetComponents(ShellRegion region);
}
