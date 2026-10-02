using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// The sync log: everything the engine, scheduler and connectivity service do, for the signed in user.
/// </summary>
public interface ISyncEventLog
{
    /// <summary>
    /// Raised after entries are written, so the Sync page can update live.
    /// </summary>
    event EventHandler? EventsWritten;

    /// <summary>
    /// Writes entries.
    /// </summary>
    /// <param name="events">The entries.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteAsync(IEnumerable<SyncEvent> events);

    /// <summary>
    /// Records a pass that changed nothing. Consecutive ones collapse into one entry with a count and the last time.
    /// </summary>
    /// <param name="owner">The user id.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="passId">The pass id.</param>
    /// <param name="timestamp">When the pass finished.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RecordNoChangePassAsync(string owner, string profile, string passId, DateTimeOffset timestamp);

    /// <summary>
    /// Reads the signed in user's entries, newest first.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>A page of entries.</returns>
    Task<SyncEventPage> QueryAsync(SyncEventQuery query);

    /// <summary>
    /// Deletes the signed in user's entries.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ClearAsync();

    /// <summary>
    /// Deletes the signed in user's entries older than the retention age, and the oldest beyond the retention count.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PruneAsync();
}
