namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// A section taken off the Settings page. Added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.RemoveSettingsSection{TComponent}"/>.
/// </summary>
public sealed class SettingsSectionRemoval
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsSectionRemoval"/> class.
    /// </summary>
    /// <param name="componentType">The component showing the section.</param>
    public SettingsSectionRemoval(Type componentType)
    {
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
    }

    /// <summary>
    /// Gets the component showing the section.
    /// </summary>
    public Type ComponentType { get; }
}
