using Devoctomy.Blazwiz.Sync.Exceptions;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Short-lived objects in the user's prefix of the remote store, under <see cref="Configuration.SyncDefaults.ScratchFolder"/>,
/// which no sync profile matches, so sync never sees them on any device. Used to time an upload, e.g. to estimate how
/// long a passphrase change takes; whatever is written is deleted again.
/// </summary>
public interface ISyncScratchStore
{
    /// <summary>
    /// Uploads an object.
    /// </summary>
    /// <param name="name">The object's name within the scratch folder.</param>
    /// <param name="content">The content.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="SyncNetworkException">The remote could not be reached.</exception>
    /// <exception cref="SyncAccessDeniedException">There is no remote, or the credentials were refused.</exception>
    Task PutAsync(string name, byte[] content, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object. Does nothing if it is already gone.
    /// </summary>
    /// <param name="name">The object's name within the scratch folder.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="SyncNetworkException">The remote could not be reached.</exception>
    /// <exception cref="SyncAccessDeniedException">There is no remote, or the credentials were refused.</exception>
    Task DeleteAsync(string name, CancellationToken ct = default);
}
