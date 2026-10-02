namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Hashes file content, to tell whether it changed.
/// </summary>
public interface IContentHasher
{
    /// <summary>
    /// Hashes content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The hash as lower case hex.</returns>
    string ComputeHash(byte[] content);
}
