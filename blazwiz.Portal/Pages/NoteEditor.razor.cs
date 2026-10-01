using Blazing.Mvvm.Components;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Notes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Pages;

/// <summary>
/// Code-behind for the note editor. Logic lives in <see cref="NoteEditorViewModel"/>; this wires up the route
/// parameter, keyboard shortcuts and the unsaved changes check when navigating away.
/// </summary>
public partial class NoteEditor : MvvmComponentBase<NoteEditorViewModel>
{
    private DotNetObjectReference<NoteEditor>? selfReference;
    private bool shortcutsAttached;

    /// <summary>
    /// Gets or sets the id of the note to edit (its key), from the <c>note</c> query string parameter. A query string,
    /// because a path ending in <c>.md</c> would be treated as a file by static hosting and not reach the app.
    /// </summary>
    [SupplyParameterFromQuery(Name = NoteEditorViewModel.NoteQueryParameter)]
    public string? NoteId { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// Handles Ctrl+S, Ctrl+B and Ctrl+I, called from <c>notesInterop.editor.attachShortcuts</c>.
    /// </summary>
    /// <param name="key">The key pressed with Ctrl (or Cmd).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [JSInvokable]
    public Task OnShortcut(string key)
    {
        return key switch
        {
            "s" => ViewModel.SaveCommand.ExecuteAsync(null),
            "b" => ViewModel.FormatCommand.ExecuteAsync(MarkdownFormat.Bold),
            "i" => ViewModel.FormatCommand.ExecuteAsync(MarkdownFormat.Italic),
            _ => Task.CompletedTask,
        };
    }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await ViewModel.LoadAsync(NoteId ?? string.Empty);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // The editor only exists once the note has loaded.
        if (!shortcutsAttached && ViewModel.Note != null)
        {
            shortcutsAttached = true;
            selfReference = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("notesInterop.editor.attachShortcuts", NoteEditorViewModel.EditorElementId, selfReference);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (shortcutsAttached && JSRuntime is IJSInProcessRuntime inProcessRuntime)
            {
                inProcessRuntime.InvokeVoid("notesInterop.editor.detachShortcuts", NoteEditorViewModel.EditorElementId);
            }

            selfReference?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnBeforeInternalNavigation(LocationChangingContext context)
    {
        if (!ViewModel.CanLeave(context.TargetLocation))
        {
            context.PreventNavigation();
        }
    }
}
