namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// A storage provider for note content. Each note's index entry records which provider holds its content,
/// so new providers (e.g. the Origin Private File System) can be added alongside existing ones.
/// </summary>
public interface INoteContentStore
{
    /// <summary>
    /// Gets the provider name recorded in the file index, see <see cref="Models.Notes.NoteStorageProviders"/>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Reads a note's content.
    /// </summary>
    /// <param name="id">The note id.</param>
    /// <returns>The markdown content, or null if the provider has none for the note.</returns>
    Task<string?> ReadAsync(string id);

    /// <summary>
    /// Writes a note's content.
    /// </summary>
    /// <param name="id">The note id.</param>
    /// <param name="content">The markdown content.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteAsync(string id, string content);

    /// <summary>
    /// Deletes a note's content.
    /// </summary>
    /// <param name="id">The note id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string id);
}
