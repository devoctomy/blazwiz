namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// What an encrypted file of a protected profile is bound to (format version 2): its key scope and its path. Both are
/// authenticated, so the file does not decrypt if it is moved to another path or another profile.
/// </summary>
public sealed record E2eFileBinding
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eFileBinding"/> class.
    /// </summary>
    /// <param name="scopeId">The key scope's id, written to the file's header.</param>
    /// <param name="path">The file's key (path), authenticated but not written to the file.</param>
    public E2eFileBinding(string scopeId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentNullException.ThrowIfNull(path);

        ScopeId = scopeId;
        Path = path;
    }

    /// <summary>
    /// Gets the key scope's id.
    /// </summary>
    public string ScopeId { get; }

    /// <summary>
    /// Gets the file's key (path).
    /// </summary>
    public string Path { get; }
}
