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
    /// Focuses (and selects the text of) the first input, select or textarea in an element.
    /// </summary>
    /// <param name="element">The element, e.g. a dialog.</param>
    /// <returns><see langword="true"/> if one was found and focused.</returns>
    Task<bool> FocusFirstInputAsync(ElementReference element);

    /// <summary>
    /// Focuses the first button in an element's modal footer (<c>.modal-footer</c>).
    /// </summary>
    /// <param name="element">The element, e.g. a dialog.</param>
    /// <returns><see langword="true"/> if one was found and focused.</returns>
    Task<bool> FocusFirstButtonAsync(ElementReference element);

    /// <summary>
    /// Focuses the first element marked <c>autofocus</c> in an element.
    /// </summary>
    /// <param name="element">The element, e.g. a dialog.</param>
    /// <returns><see langword="true"/> if one was found and focused.</returns>
    Task<bool> FocusAutofocusAsync(ElementReference element);

    /// <summary>
    /// Focuses an element itself. It needs a <c>tabindex</c> unless it is focusable already.
    /// </summary>
    /// <param name="element">The element, e.g. a dialog.</param>
    /// <returns><see langword="true"/> if the element exists and was focused.</returns>
    Task<bool> FocusAsync(ElementReference element);
}
