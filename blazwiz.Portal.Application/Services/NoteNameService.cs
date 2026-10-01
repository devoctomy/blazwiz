using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class NoteNameService
    : INoteNameService
{
    // Characters that are not allowed in file names on common file systems, plus those S3 advises against in keys, so
    // names survive being synced or exported.
    private static readonly char[] InvalidCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '#', '%', '{', '}', '^', '~', '[', ']', '`'];

    /// <inheritdoc/>
    public string NoteExtension => ".md";

    /// <inheritdoc/>
    public int MaxNameLength => 100;

    /// <inheritdoc/>
    public string? Validate(string? name)
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
            return "Names cannot contain any of these characters: / \\ : * ? \" < > | # % { } ^ ~ [ ] `";
        }

        if (trimmed.Trim('.').Length == 0)
        {
            return "Please enter a name.";
        }

        return null;
    }

    /// <inheritdoc/>
    public string ToNoteFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return trimmed.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^NoteExtension.Length] + NoteExtension
            : trimmed + NoteExtension;
    }

    /// <inheritdoc/>
    public string WithoutExtension(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return fileName.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^NoteExtension.Length]
            : fileName;
    }
}
