namespace MyApp.Portal.Models.Notes;

/// <summary>
/// A visible row in the files tree.
/// </summary>
public class FileTreeItem
{
    /// <summary>
    /// Gets or sets the index entry shown in the row.
    /// </summary>
    public NoteEntry Entry { get; set; } = new NoteEntry();

    /// <summary>
    /// Gets or sets how deeply the entry is nested. Zero at the top level.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the folder is expanded. Always false for notes.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Gets or sets the number of entries directly inside the folder. Always zero for notes.
    /// </summary>
    public int ChildCount { get; set; }
}
