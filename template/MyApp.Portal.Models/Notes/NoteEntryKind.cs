using System.Text.Json.Serialization;

namespace MyApp.Portal.Models.Notes;

/// <summary>
/// The kind of entry in the notes file index.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<NoteEntryKind>))]
public enum NoteEntryKind
{
    /// <summary>
    /// A folder, which can contain notes and other folders.
    /// </summary>
    Folder,

    /// <summary>
    /// A markdown note.
    /// </summary>
    Note,
}
