using blazwiz.Core.Sync.FileTree;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Access to the browser's origin private file system (OPFS), through <c>wwwroot/js/opfs.js</c>. Files are kept under
/// <c>/&lt;root&gt;/&lt;owner&gt;/&lt;key&gt;</c>; keys ending with <c>/</c> are folder markers.
/// </summary>
public interface IOpfsClient
{
    /// <summary>
    /// Gets whether the browser has OPFS (not, e.g., some private browsing modes).
    /// </summary>
    /// <returns>True if OPFS can be used.</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// Lists a user's files and folder markers.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="owner">The user id.</param>
    /// <returns>The files, in no particular order.</returns>
    Task<IReadOnlyList<FileTreeEntry>> ListAsync(string root, string owner);

    /// <summary>
    /// Reads a file.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="key">The key.</param>
    /// <returns>The file, or null if there is none.</returns>
    Task<FileTreeFile?> ReadAsync(string root, string owner, string key);

    /// <summary>
    /// Adds or replaces a file, creating its directories.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="key">The key.</param>
    /// <param name="content">The content (ignored for folder markers).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteAsync(string root, string owner, string key, byte[] content);

    /// <summary>
    /// Deletes a file, then any directories left empty. Does nothing if it does not exist.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="key">The key.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string root, string owner, string key);
}
