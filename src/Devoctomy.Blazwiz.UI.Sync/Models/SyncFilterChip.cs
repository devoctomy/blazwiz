namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// A filter chip on the Sync page.
/// </summary>
public class SyncFilterChip
{
    /// <summary>
    /// Gets or sets the group the chip shows, or null for All.
    /// </summary>
    public SyncEventGroup? Group { get; set; }

    /// <summary>
    /// Gets or sets the chip's label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the chip is selected.
    /// </summary>
    public bool IsActive { get; set; }
}
