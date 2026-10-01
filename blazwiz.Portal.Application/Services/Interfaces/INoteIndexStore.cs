using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Persists the notes file index (folders and note metadata) and per user notes state.
/// </summary>
public interface INoteIndexStore
{
    /// <summary>
    /// Gets every entry belonging to a user.
    /// </summary>
    /// <param name="owner">The username.</param>
    /// <returns>The user's entries, in no particular order.</returns>
    Task<IReadOnlyList<NoteEntry>> GetEntriesAsync(string owner);

    /// <summary>
    /// Adds or replaces an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveEntryAsync(NoteEntry entry);

    /// <summary>
    /// Removes entries.
    /// </summary>
    /// <param name="ids">The ids of the entries to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteEntriesAsync(IEnumerable<string> ids);

    /// <summary>
    /// Gets a user's notes state.
    /// </summary>
    /// <param name="owner">The username.</param>
    /// <returns>The state, or null if none has been saved.</returns>
    Task<NotesState?> GetStateAsync(string owner);

    /// <summary>
    /// Saves a user's notes state.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveStateAsync(NotesState state);
}
