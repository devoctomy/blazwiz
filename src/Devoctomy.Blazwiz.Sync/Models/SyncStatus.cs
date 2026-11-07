namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// The sync service's current state, for the status pill, Settings and the Sync page. It covers every profile: each
/// one's last pass counts, not only the last pass the service ran.
/// </summary>
public class SyncStatus
{
    /// <summary>
    /// Gets or sets what the service is doing.
    /// </summary>
    public SyncState State { get; set; }

    /// <summary>
    /// Gets or sets when the least recently synced profile last synced successfully, or null while any profile has not
    /// yet this session.
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Gets or sets why a profile's last pass failed, as an error code (see <see cref="Exceptions.SyncErrorCodes"/>), or
    /// null if none did. With more than one, it is the first profile's. The UI shows it with the text resource
    /// <c>Error.&lt;code&gt;</c>.
    /// </summary>
    public string? LastErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the values the last error's text needs, e.g. how many files failed.
    /// </summary>
    public IReadOnlyList<object> LastErrorArguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of local changes waiting to be synced.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of conflicts resolved by each profile's last pass, added up.
    /// </summary>
    public int ConflictsResolved { get; set; }

    /// <summary>
    /// Creates a copy of the status.
    /// </summary>
    /// <returns>The copy.</returns>
    public SyncStatus Clone() => (SyncStatus)MemberwiseClone();
}
