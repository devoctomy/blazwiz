namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// Validation and normalisation of note and folder names.
/// </summary>
public interface INoteNameService
{
    /// <summary>
    /// Gets the file extension given to every note.
    /// </summary>
    string NoteExtension { get; }

    /// <summary>
    /// Gets the longest name allowed, including the extension.
    /// </summary>
    int MaxNameLength { get; }

    /// <summary>
    /// Checks a name entered by the user.
    /// </summary>
    /// <param name="name">The name, which is trimmed first.</param>
    /// <returns>An error message to show, or null when the name is valid.</returns>
    string? Validate(string? name);

    /// <summary>
    /// Trims a note name and adds the <c>.md</c> extension if it is missing.
    /// </summary>
    /// <param name="name">The name entered by the user.</param>
    /// <returns>The note's file name.</returns>
    string ToNoteFileName(string name);

    /// <summary>
    /// Gets a note's name without the <c>.md</c> extension, for editing.
    /// </summary>
    /// <param name="fileName">The note's file name.</param>
    /// <returns>The name without the extension.</returns>
    string WithoutExtension(string fileName);
}
