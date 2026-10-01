namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// A note loaded for editing: its index entry, content and location.
/// </summary>
public class NoteDocument
{
    /// <summary>
    /// Gets or sets the note's index entry.
    /// </summary>
    public NoteEntry Entry { get; set; } = new NoteEntry();

    /// <summary>
    /// Gets or sets the markdown content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the names of the folders containing the note, outermost first. Empty at the top level.
    /// </summary>
    public IReadOnlyList<string> FolderPath { get; set; } = [];
}
