namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// A folder that an entry can be moved into, for the move dialog.
/// </summary>
public class FolderOption
{
    /// <summary>
    /// Gets or sets the folder id, or an empty string for the top level.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the folder name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how deeply the folder is nested, for indenting. Zero for the top level.
    /// </summary>
    public int Depth { get; set; }
}
