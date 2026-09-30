using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Components;

/// <summary>
/// A themed Bootstrap modal dialog. Render it while the dialog should be open; it focuses its first input
/// (or button) when shown and calls <see cref="OnClose"/> when Escape or the close button is pressed.
/// </summary>
public partial class ModalDialog : ComponentBase
{
    private ElementReference element;

    /// <summary>
    /// Gets or sets the element id, also used to label the dialog with its title.
    /// </summary>
    [Parameter]
    public string Id { get; set; } = "modal-dialog";

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional Bootstrap icon class shown before the title, e.g. <c>bi-folder</c>.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the optional Bootstrap modal size class, e.g. <c>modal-sm</c> or <c>modal-lg</c>.
    /// </summary>
    [Parameter]
    public string? SizeClass { get; set; }

    /// <summary>
    /// Gets or sets the body of the dialog.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the footer, typically the action buttons.
    /// </summary>
    [Parameter]
    public RenderFragment? Footer { get; set; }

    /// <summary>
    /// Gets or sets what happens when the dialog is dismissed with Escape or the close button.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("portalInterop.focusFirstInput", element);
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await OnClose.InvokeAsync();
        }
    }
}
