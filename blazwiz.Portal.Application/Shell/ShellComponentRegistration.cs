namespace blazwiz.Portal.Application.Shell;

/// <summary>
/// A component shown in a region of the app shell. Added with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddShellComponent{TComponent}"/>.
/// </summary>
public sealed class ShellComponentRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellComponentRegistration"/> class.
    /// </summary>
    /// <param name="region">Where it is shown.</param>
    /// <param name="componentType">The component.</param>
    /// <param name="order">Its position in the region: lower comes first.</param>
    public ShellComponentRegistration(ShellRegion region, Type componentType, int order)
    {
        Region = region;
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        Order = order;
    }

    /// <summary>
    /// Gets where it is shown.
    /// </summary>
    public ShellRegion Region { get; }

    /// <summary>
    /// Gets the component.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets its position in the region: lower comes first.
    /// </summary>
    public int Order { get; }
}
