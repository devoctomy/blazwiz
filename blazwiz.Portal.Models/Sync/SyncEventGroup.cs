namespace blazwiz.Portal.Models.Sync;

/// <summary>
/// The filter chips on the Sync page, grouping sync log entries.
/// </summary>
public enum SyncEventGroup
{
    /// <summary>
    /// Passes starting and finishing, retries and files already in sync.
    /// </summary>
    Activity,

    /// <summary>
    /// Files uploaded.
    /// </summary>
    Uploads,

    /// <summary>
    /// Files downloaded.
    /// </summary>
    Downloads,

    /// <summary>
    /// Files deleted on either side.
    /// </summary>
    Deletes,

    /// <summary>
    /// Files changed on both sides.
    /// </summary>
    Conflicts,

    /// <summary>
    /// Failures.
    /// </summary>
    Errors,

    /// <summary>
    /// Going offline or online, and offline mode.
    /// </summary>
    Connectivity,
}
