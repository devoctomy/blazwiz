namespace blazwiz.Portal.Application.Helpers;

/// <summary>
/// Validation and normalisation of note and folder names.
/// </summary>
public static class NoteNameHelpers
{
    /// <summary>
    /// The file extension given to every note.
    /// </summary>
    public const string NoteExtension = ".md";

    /// <summary>
    /// The longest name allowed, including the extension.
    /// </summary>
    public const int MaxNameLength = 100;

    // Characters that are not allowed in file names on common file systems, so names survive being synced or exported.
    private static readonly char[] InvalidCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Checks a name entered by the user.
    /// </summary>
    /// <param name="name">The name, which is trimmed first.</param>
    /// <returns>An error message to show, or null when the name is valid.</returns>
    public static string? Validate(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "Please enter a name.";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"Names can be at most {MaxNameLength} characters.";
        }

        if (trimmed.IndexOfAny(InvalidCharacters) >= 0 || trimmed.Any(char.IsControl))
        {
            return "Names cannot contain any of these characters: / \\ : * ? \" < > |";
        }

        if (trimmed.Trim('.').Length == 0)
        {
            return "Please enter a name.";
        }

        return null;
    }

    /// <summary>
    /// Trims a note name and adds the <c>.md</c> extension if it is missing.
    /// </summary>
    /// <param name="name">The name entered by the user.</param>
    /// <returns>The note's file name.</returns>
    public static string ToNoteFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return trimmed.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^NoteExtension.Length] + NoteExtension
            : trimmed + NoteExtension;
    }

    /// <summary>
    /// Gets a note's name without the <c>.md</c> extension, for editing.
    /// </summary>
    /// <param name="fileName">The note's file name.</param>
    /// <returns>The name without the extension.</returns>
    public static string WithoutExtension(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return fileName.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^NoteExtension.Length]
            : fileName;
    }
}
