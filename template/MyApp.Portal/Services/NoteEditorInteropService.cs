using Microsoft.JSInterop;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Services;

/// <summary>
/// Implements <see cref="INoteEditorInteropService"/> using the functions in <c>wwwroot/js/notes.js</c>.
/// </summary>
public class NoteEditorInteropService(IJSRuntime jsRuntime) : INoteEditorInteropService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <inheritdoc/>
    public async Task<EditorSelection?> GetSelectionAsync(string elementId)
    {
        return await jsRuntime.InvokeAsync<EditorSelection?>("notesInterop.editor.getSelection", elementId);
    }

    /// <inheritdoc/>
    public async Task ApplyEditAsync(string elementId, MarkdownTextEdit edit)
    {
        await jsRuntime.InvokeVoidAsync("notesInterop.editor.applyEdit", elementId, edit);
    }
}
