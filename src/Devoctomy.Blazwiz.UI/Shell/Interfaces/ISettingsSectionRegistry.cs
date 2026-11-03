namespace Devoctomy.Blazwiz.UI.Shell.Interfaces;

/// <summary>
/// Says which sections the Settings page shows.
/// </summary>
public interface ISettingsSectionRegistry
{
    /// <summary>
    /// Gets the sections, in order.
    /// </summary>
    /// <returns>The component types showing each section.</returns>
    IReadOnlyList<Type> GetSections();
}
