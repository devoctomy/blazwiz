using Blazing.Mvvm.Components;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// Shows a profile's content while it is unlocked, and in its place, while it is locked, a panel asking for its
/// passphrase (or pointing to Settings while it has none). Wrap any page's content of a profile in it:
/// <c>&lt;E2eUnlockPanel Profile="notes"&gt;...&lt;/E2eUnlockPanel&gt;</c>. Logic lives in
/// <see cref="E2eUnlockPanelViewModel"/>.
/// </summary>
public partial class E2eUnlockPanel : MvvmComponentBase<E2eUnlockPanelViewModel>
{
    /// <summary>
    /// Gets or sets the sync profile's name.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile's content, shown while it is unlocked.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await ViewModel.SetProfileAsync(Profile);
    }
}
