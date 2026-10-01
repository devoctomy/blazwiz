namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// The content of a note as held by a storage provider.
/// </summary>
public class NoteContent
{
    /// <summary>
    /// Gets or sets the id of the note (see <see cref="NoteEntry.Id"/>).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the markdown content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
