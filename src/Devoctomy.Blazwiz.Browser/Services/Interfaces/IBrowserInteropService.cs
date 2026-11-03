using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.Browser.Services.Interfaces;

/// <summary>
/// Abstraction over browser specific functionality implemented in JavaScript.
/// </summary>
public interface IBrowserInteropService
{
    /// <summary>
    /// Applies the given theme to the document.
    /// </summary>
    /// <param name="theme">The theme name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetThemeAsync(string theme);

    /// <summary>
    /// Gets the operating system preferred theme (<c>light</c> or <c>dark</c>).
    /// </summary>
    /// <returns>The preferred theme name.</returns>
    Task<string> GetPreferredThemeAsync();

    /// <summary>
    /// Sets the document's language (<c>lang</c> on <c>&lt;html&gt;</c>), e.g. <c>fr-FR</c>.
    /// </summary>
    /// <param name="language">The language tag.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetDocumentLanguageAsync(string language);

    /// <summary>
    /// Focuses (and selects the text of) the first input in an element, or its first button if it has no inputs.
    /// </summary>
    /// <param name="element">The element, e.g. a dialog.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task FocusFirstInputAsync(ElementReference element);
}
