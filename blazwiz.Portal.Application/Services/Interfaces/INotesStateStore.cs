using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Keeps per user state of the notes feature on this device (it is not synced), e.g. whether the welcome note has been created.
/// </summary>
public interface INotesStateStore
{
    /// <summary>
    /// Gets a user's notes state.
    /// </summary>
    /// <param name="owner">The username.</param>
    /// <returns>The state, or null if none has been saved.</returns>
    Task<NotesState?> GetAsync(string owner);

    /// <summary>
    /// Saves a user's notes state.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(NotesState state);
}
