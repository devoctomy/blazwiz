using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The remote side of a sync profile. Keys are relative to the user's root, and only keys matching the profile's
/// pattern are listed. ETags are opaque.
/// </summary>
public interface ISyncRemoteStore
{
    /// <summary>
    /// Lists the objects matching the profile's pattern.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The objects. <see cref="RemoteObjectInfo.ModifiedAt"/> is not known from a listing.</returns>
    /// <exception cref="SyncNetworkException">The remote could not be reached.</exception>
    /// <exception cref="SyncAccessDeniedException">The credentials were refused.</exception>
    Task<IReadOnlyList<RemoteObjectInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads an object.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The object, or null if it does not exist.</returns>
    Task<RemoteObject?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Reads an object's ETag and metadata without its content (HEAD). Only used for conflicts.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The object's details, or null if it does not exist.</returns>
    Task<RemoteObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Uploads an object, recording <paramref name="modifiedAt"/> in its metadata.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="content">The content.</param>
    /// <param name="modifiedAt">When the file was last edited.</param>
    /// <param name="ifMatch">Only upload if the object's ETag is this one, or null to not check.</param>
    /// <param name="ifNoneMatch">Only upload if the object does not exist yet.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new ETag.</returns>
    /// <exception cref="SyncPreconditionFailedException">The object changed since it was listed (412).</exception>
    Task<string> PutAsync(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object. Does nothing if it is already gone.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ifMatch">Only delete if the object's ETag is this one, or null to not check.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="SyncPreconditionFailedException">The object changed since it was listed (412).</exception>
    Task DeleteAsync(string key, string? ifMatch, CancellationToken ct = default);
}
