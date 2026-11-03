using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// Reads and changes the text and selection of the note editor in the browser.
/// </summary>
public interface INoteEditorInteropService
{
    /// <summary>
    /// Gets the editor's text and selection.
    /// </summary>
    /// <param name="elementId">The id of the editor's textarea.</param>
    /// <returns>The text and selection, or null if the editor is not on the page.</returns>
    Task<EditorSelection?> GetSelectionAsync(string elementId);

    /// <summary>
    /// Applies an edit to the editor, keeping the browser's undo history where possible, then selects the result.
    /// </summary>
    /// <param name="elementId">The id of the editor's textarea.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ApplyEditAsync(string elementId, MarkdownTextEdit edit);
}
