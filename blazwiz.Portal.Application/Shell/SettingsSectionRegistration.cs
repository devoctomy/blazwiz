namespace blazwiz.Portal.Application.Shell;

/// <summary>
/// A section of the Settings page. Added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddSettingsSection{TComponent}"/>.
/// </summary>
public sealed class SettingsSectionRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsSectionRegistration"/> class.
    /// </summary>
    /// <param name="componentType">The component showing the section.</param>
    /// <param name="order">Its position on the page: lower comes first.</param>
    public SettingsSectionRegistration(Type componentType, int order)
    {
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        Order = order;
    }

    /// <summary>
    /// Gets the component showing the section.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets its position on the page: lower comes first.
    /// </summary>
    public int Order { get; }
}
