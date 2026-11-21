using Blazing.Mvvm.Components;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// The change passphrase dialog for one profile, for an app to open from its own pages:
/// <c>&lt;E2eChangePassphraseDialog Profile="@id" OnClose="..." /&gt;</c>, rendered while it should be open. It is the
/// dialog Settings shows: the current and new passphrase with the Advanced tab, the estimate, the progress, and signing
/// every device out at the end. Logic lives in <see cref="E2eProfilesViewModel"/>.
/// </summary>
public partial class E2eChangePassphraseDialog : MvvmComponentBase<E2eProfilesViewModel>
{
    /// <summary>
    /// Gets or sets the sync profile's name.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what happens when the dialog is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        // The view model reads the user's profiles first, so the one named can be found.
        await base.OnInitializedAsync();
        await ViewModel.OpenChangePassphraseAsync(Profile);
    }
}
