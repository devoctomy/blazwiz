using Blazing.Mvvm.Components;
using Devoctomy.Blazwiz.UI.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Components;

/// <summary>
/// A themed Bootstrap modal dialog. Render it while the dialog should be open; it focuses what
/// <see cref="FocusMode"/> says when shown and calls <see cref="OnClose"/> when Escape or the close button is pressed.
/// Logic lives in <see cref="ModalDialogViewModel"/>.
/// </summary>
public partial class ModalDialog : MvvmComponentBase<ModalDialogViewModel>
{
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
    /// Gets or sets what is focused when the dialog is shown. Defaults to <see cref="ModalDialogFocusMode.FirstInput"/>.
    /// </summary>
    [Parameter]
    public ModalDialogFocusMode FocusMode { get; set; } = ModalDialogFocusMode.FirstInput;

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

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ViewModel.SetParameters(FocusMode, OnClose);
    }
}
