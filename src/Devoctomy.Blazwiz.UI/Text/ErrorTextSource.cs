namespace Devoctomy.Blazwiz.UI.Text;

/// <summary>
/// Resources holding the text for a module's error codes (keys <c>Error.&lt;code&gt;</c>). Added with
/// <see cref="Extensions.TextUiServiceCollectionExtensions.AddErrorText{TResource}"/>.
/// </summary>
public sealed class ErrorTextSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTextSource"/> class.
    /// </summary>
    /// <param name="resourceType">The class the resources are named after.</param>
    public ErrorTextSource(Type resourceType)
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
    }

    /// <summary>
    /// Gets the class the resources are named after.
    /// </summary>
    public Type ResourceType { get; }
}
