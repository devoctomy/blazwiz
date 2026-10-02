namespace blazwiz.Portal.Models.Sync;

/// <summary>
/// How a kind of sync log entry is shown.
/// </summary>
public class SyncEventPresentation
{
    /// <summary>
    /// Gets or sets the Bootstrap Icons class, e.g. <c>bi-cloud-arrow-up</c>.
    /// </summary>
    public string Icon { get; set; } = "bi-circle";

    /// <summary>
    /// Gets or sets the CSS class for the icon's colour (a theme aware Bootstrap text class).
    /// </summary>
    public string ColorClass { get; set; } = "text-body-secondary";

    /// <summary>
    /// Gets or sets a short label, e.g. "Uploaded".
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter group.
    /// </summary>
    public SyncEventGroup Group { get; set; }
}
