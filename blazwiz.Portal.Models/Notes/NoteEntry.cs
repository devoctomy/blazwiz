using System.Text.Json.Serialization;

namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// A folder or note, built from the keys in the notes file store. Notes are plain markdown files and folders are
/// paths, so the id is the file's key (e.g. <c>notes/Work/Ideas.md</c>, or <c>notes/Work/</c> for a folder).
/// </summary>
public class NoteEntry
{
    /// <summary>
    /// Gets or sets the entry's key, which is its id. Folder keys end with <c>/</c>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username of the user the entry belongs to.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the entry is a folder or a note.
    /// </summary>
    public NoteEntryKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the user. Notes always end with <c>.md</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key of the folder containing the entry, or null when it is at the top level.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Gets or sets when the entry was created. Only known for entries migrated from earlier versions; otherwise the
    /// same as <see cref="ModifiedAt"/>.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the entry (or the note's content) was last changed.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the entry is a folder.
    /// </summary>
    [JsonIgnore]
    public bool IsFolder => Kind == NoteEntryKind.Folder;
}
