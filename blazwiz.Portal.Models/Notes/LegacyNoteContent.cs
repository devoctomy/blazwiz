namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// Note content as kept by earlier versions of the app (the <c>content</c> object store), read once to migrate it to
/// files.
/// </summary>
public class LegacyNoteContent
{
    /// <summary>
    /// Gets or sets the id of the note.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the markdown content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
