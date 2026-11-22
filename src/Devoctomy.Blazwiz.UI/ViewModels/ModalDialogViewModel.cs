using Blazing.Mvvm.ComponentModel;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for <see cref="ModalDialog"/>: focuses the dialog as its <see cref="ModalDialogFocusMode"/> says when it
/// is shown, and closes it on Escape.
/// </summary>
public class ModalDialogViewModel : ViewModelBase
{
    private readonly IBrowserInteropService browserInterop;
    private ModalDialogFocusMode focusMode;
    private EventCallback onClose;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModalDialogViewModel"/> class.
    /// </summary>
    /// <param name="browserInterop">Browser interop, which focuses elements.</param>
    public ModalDialogViewModel(IBrowserInteropService browserInterop)
    {
        this.browserInterop = browserInterop ?? throw new ArgumentNullException(nameof(browserInterop));
    }

    /// <summary>
    /// Gets or sets the dialog element, set by the view.
    /// </summary>
    public ElementReference Element { get; set; }

    /// <summary>
    /// Takes the dialog's parameters.
    /// </summary>
    /// <param name="focusMode">What to focus when the dialog is shown.</param>
    /// <param name="onClose">What happens when the dialog is dismissed with Escape.</param>
    public void SetParameters(ModalDialogFocusMode focusMode, EventCallback onClose)
    {
        this.focusMode = focusMode;
        this.onClose = onClose;
    }

    /// <inheritdoc/>
    public override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await FocusAsync();
        }
    }

    /// <summary>
    /// Closes the dialog when Escape is pressed.
    /// </summary>
    /// <param name="e">The key pressed.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == "Escape")
        {
            await onClose.InvokeAsync();
        }
    }

    private async Task FocusAsync()
    {
        var focused = focusMode switch
        {
            ModalDialogFocusMode.None => true,
            ModalDialogFocusMode.FirstInput => await browserInterop.FocusFirstInputAsync(Element),
            ModalDialogFocusMode.FirstButton => await browserInterop.FocusFirstButtonAsync(Element),
            ModalDialogFocusMode.Autofocus => await browserInterop.FocusAutofocusAsync(Element),
            _ => false,
        };
        if (!focused)
        {
            await browserInterop.FocusAsync(Element);
        }
    }
}
