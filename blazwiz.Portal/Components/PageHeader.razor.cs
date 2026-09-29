using blazwiz.Portal.Models.Settings;
using Microsoft.AspNetCore.Components;

namespace blazwiz.Portal.Components;

/// <summary>
/// Standard page heading with optional icon, subtitle and action buttons. Also sets the browser tab title.
/// </summary>
public partial class PageHeader : ComponentBase
{
    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional subtitle.
    /// </summary>
    [Parameter]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the optional Bootstrap icon class, e.g. <c>bi-gear</c>.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets optional content (e.g. action buttons) shown on the right.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private AppSettings AppSettings { get; set; } = default!;
}
