using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Where a profile's sync index is kept: one <see cref="SyncIndexRecord"/> per profile, user and key.
/// </summary>
public interface ISyncIndexStore
{
    /// <summary>
    /// Gets every record for a profile and user.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The records, in no particular order.</returns>
    Task<IReadOnlyList<SyncIndexRecord>> GetAllAsync(string profile, string owner, CancellationToken ct = default);

    /// <summary>
    /// Gets one record.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="key">The file's key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The record, or null if there is none.</returns>
    Task<SyncIndexRecord?> GetAsync(string profile, string owner, string key, CancellationToken ct = default);

    /// <summary>
    /// Adds or replaces a record.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(SyncIndexRecord record, CancellationToken ct = default);

    /// <summary>
    /// Removes a record. Does nothing if there is none.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="owner">The user id.</param>
    /// <param name="key">The file's key.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(string profile, string owner, string key, CancellationToken ct = default);
}
