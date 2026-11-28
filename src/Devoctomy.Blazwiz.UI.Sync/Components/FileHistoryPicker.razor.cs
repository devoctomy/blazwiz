using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// A synced file's History dropdown, for the top of the page showing the file: <b>Current</b>, then each version in the
/// bucket as its ETag and upload time, listed each time it is expanded. It also holds the dialogs asking whether to save
/// the page's changes first, and confirming a restore. Put <see cref="FileHistoryBar"/> above the file's content, and
/// show <see cref="FileHistoryViewModel.SelectedContent"/>, read only, while
/// <see cref="FileHistoryViewModel.IsViewingVersion"/>. It shows the view model it is given, owned by the page's view
/// model, and is shown again whenever the page is. Hidden without a bucket. Logic lives in
/// <see cref="FileHistoryViewModel"/>.
/// </summary>
public partial class FileHistoryPicker : ComponentBase
{
    /// <summary>
    /// Gets or sets the file's history.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public FileHistoryViewModel Model { get; set; } = default!;

    /// <summary>
    /// Gets or sets what every element id here starts with.
    /// </summary>
    [Parameter]
    public string IdPrefix { get; set; } = "file-history";
}
