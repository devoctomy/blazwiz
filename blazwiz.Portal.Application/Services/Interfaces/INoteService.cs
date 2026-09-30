using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Manages the signed in user's notes and folders. Operations that cannot be completed (e.g. a duplicate name)
/// throw a <see cref="NotesException"/> with a message suitable for the user.
/// </summary>
public interface INoteService
{
    /// <summary>
    /// Gets all of the user's entries. The first time this is called for a user, a welcome note is created.
    /// </summary>
    /// <returns>The user's entries, in no particular order.</returns>
    Task<IReadOnlyList<NoteEntry>> GetEntriesAsync();

    /// <summary>
    /// Creates a folder.
    /// </summary>
    /// <param name="name">The folder name.</param>
    /// <param name="parentId">The containing folder id, or null for the top level.</param>
    /// <returns>The new folder.</returns>
    Task<NoteEntry> CreateFolderAsync(string name, string? parentId);

    /// <summary>
    /// Creates a note. <c>.md</c> is added to the name if it is missing.
    /// </summary>
    /// <param name="name">The note name.</param>
    /// <param name="parentId">The containing folder id, or null for the top level.</param>
    /// <param name="content">The initial markdown content.</param>
    /// <returns>The new note.</returns>
    Task<NoteEntry> CreateNoteAsync(string name, string? parentId, string content = "");

    /// <summary>
    /// Renames a note or folder. <c>.md</c> is added to note names if it is missing.
    /// </summary>
    /// <param name="id">The entry id.</param>
    /// <param name="name">The new name.</param>
    /// <returns>The updated entry.</returns>
    Task<NoteEntry> RenameAsync(string id, string name);

    /// <summary>
    /// Moves a note or folder into another folder.
    /// </summary>
    /// <param name="id">The entry id.</param>
    /// <param name="parentId">The destination folder id, or null for the top level.</param>
    /// <returns>The updated entry.</returns>
    Task<NoteEntry> MoveAsync(string id, string? parentId);

    /// <summary>
    /// Deletes a note, or a folder and everything in it.
    /// </summary>
    /// <param name="id">The entry id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string id);

    /// <summary>
    /// Loads a note for editing.
    /// </summary>
    /// <param name="id">The note id.</param>
    /// <returns>The note, or null if the user has no note with that id.</returns>
    Task<NoteDocument?> GetNoteAsync(string id);

    /// <summary>
    /// Saves a note's content.
    /// </summary>
    /// <param name="id">The note id.</param>
    /// <param name="content">The markdown content.</param>
    /// <returns>The updated entry.</returns>
    Task<NoteEntry> SaveNoteAsync(string id, string content);
}
