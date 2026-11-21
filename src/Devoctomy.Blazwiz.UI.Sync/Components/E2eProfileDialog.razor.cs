using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// The dialog a profile's passphrase and keys are changed in: setting a first passphrase, creating a profile,
/// unlocking, changing the passphrase (with its estimate, progress and sign out everywhere), finishing a change cut
/// short, and deleting. It shows the dialog its view model has open, and nothing while none is. The Settings section
/// (<see cref="E2eProfilesSection"/>) uses it, and so does <see cref="E2eChangePassphraseDialog"/>. Logic lives in
/// <see cref="E2eProfilesViewModel"/>.
/// </summary>
public partial class E2eProfileDialog : ComponentBase
{
    /// <summary>
    /// Gets or sets the view model whose dialog is shown, owned by the component showing this one.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public E2eProfilesViewModel Model { get; set; } = default!;

    /// <summary>
    /// Gets or sets what happens once the dialog has closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClosed { get; set; }

    // Closing is refused while the dialog's change is running: only say it closed when it did.
    private async Task CloseAsync()
    {
        Model.CloseDialogCommand.Execute(null);
        if (Model.ActiveDialog == null)
        {
            await OnClosed.InvokeAsync();
        }
    }
}
