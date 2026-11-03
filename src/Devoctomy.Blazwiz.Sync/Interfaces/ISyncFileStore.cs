using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The local side of a sync profile: where the app reads and writes files. Keys are relative to the user's root
/// (e.g. <c>documents/Work/Ideas.md</c>), and implementations partition by user (<see cref="ISyncUserContext"/>) internally.
/// </summary>
/// <remarks>
/// App code gets the store for a profile with <c>[FromKeyedServices("&lt;profile&gt;")]</c>. That store tracks every
/// write and delete in the sync index and asks for a sync, so app code never touches the index.
/// </remarks>
public interface ISyncFileStore
{
    /// <summary>
    /// Lists the signed in user's files and folder markers.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The files, in no particular order.</returns>
    Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads a file.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The file, or null if there is none.</returns>
    Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Adds or replaces a file.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="content">The content.</param>
    /// <param name="modifiedAt">The modified time to record, or null for now.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a folder marker (an empty file whose key ends with <c>/</c>), so an empty folder is kept.
    /// </summary>
    /// <param name="key">The folder key, ending with <c>/</c>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CreateFolderAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file or folder marker. Does nothing if it does not exist.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
