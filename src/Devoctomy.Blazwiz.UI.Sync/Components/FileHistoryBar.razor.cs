using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// The bar above a file's content while a past version is shown: which version it is, with <b>Back to current</b> and
/// <b>Restore this version</b>, and why a version could not be shown or restored. Goes with
/// <see cref="FileHistoryPicker"/>, given the same view model. Logic lives in <see cref="FileHistoryViewModel"/>.
/// </summary>
public partial class FileHistoryBar : ComponentBase
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
