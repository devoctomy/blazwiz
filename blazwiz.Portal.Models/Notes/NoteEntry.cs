using System.Text.Json.Serialization;

namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// An entry (folder or note) in a user's notes file index. The index only holds metadata, the content of
/// a note is kept by the storage provider named in <see cref="StorageProvider"/>.
/// </summary>
public class NoteEntry
{
    /// <summary>
    /// Gets or sets the unique id of the entry.
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
    /// Gets or sets the id of the folder containing the entry, or null when it is at the top level.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the storage provider holding the note's content (see <see cref="NoteStorageProviders"/>). Null for folders.
    /// </summary>
    public string? StorageProvider { get; set; }

    /// <summary>
    /// Gets or sets when the entry was created.
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
