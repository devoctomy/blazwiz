namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// A language the user can choose.
/// </summary>
public class CultureOption
{
    /// <summary>
    /// Gets or sets the culture's name, e.g. <c>fr-FR</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language's name in that language, e.g. <c>Français (France)</c>.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
