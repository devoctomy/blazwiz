using blazwiz.Core.Sync.FileTree;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Access to a folder the user picks on their computer (File System Access API), through <c>wwwroot/js/folder.js</c>.
/// The picked folder is kept in the browser under a handle key (one per user) and used again after a reload; keys
/// ending with <c>/</c> are folder markers.
/// </summary>
public interface IFolderAccessClient
{
    /// <summary>
    /// Gets whether the browser can pick folders (Chromium desktop).
    /// </summary>
    /// <returns>True if it can.</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// Asks the user to pick a folder and keeps it. Must be called straight after a click.
    /// </summary>
    /// <param name="handleKey">The key to keep the folder under.</param>
    /// <returns>The folder's name, or null if the user cancelled or refused permission.</returns>
    Task<string?> PickAsync(string handleKey);

    /// <summary>
    /// Gets the kept folder and whether the app may use it now.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <returns>The status, or null if no folder has been picked.</returns>
    Task<SyncFolderStatus?> GetStatusAsync(string handleKey);

    /// <summary>
    /// Asks for permission to the kept folder again. Must be called straight after a click.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <returns>True if permission was granted.</returns>
    Task<bool> ReconnectAsync(string handleKey);

    /// <summary>
    /// Lists the files and folder markers under a prefix in the folder.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <param name="prefix">The directory to list, ending with <c>/</c> (e.g. <c>notes/</c>), or empty for the whole folder.</param>
    /// <returns>The files, in no particular order, with keys from the folder's root.</returns>
    Task<IReadOnlyList<FileTreeEntry>> ListAsync(string handleKey, string prefix);

    /// <summary>
    /// Reads a file.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <param name="key">The file's key.</param>
    /// <returns>The file, or null if there is none.</returns>
    Task<FileTreeFile?> ReadAsync(string handleKey, string key);

    /// <summary>
    /// Adds or replaces a file, creating its directories.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <param name="key">The file's key.</param>
    /// <param name="content">The content (ignored for folder markers).</param>
    /// <returns>When the file system says the file was written, in milliseconds since the Unix epoch.</returns>
    Task<long> WriteAsync(string handleKey, string key, byte[] content);

    /// <summary>
    /// Deletes a file, then any directories left empty. Does nothing if it does not exist.
    /// </summary>
    /// <param name="handleKey">The key the folder is kept under.</param>
    /// <param name="key">The file's key.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string handleKey, string key);
}
