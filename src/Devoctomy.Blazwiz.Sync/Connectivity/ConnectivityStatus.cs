namespace Devoctomy.Blazwiz.Sync.Connectivity;

/// <summary>
/// Whether the app can sync, and why not.
/// </summary>
public class ConnectivityStatus
{
    /// <summary>
    /// Gets or sets a value indicating whether the app is online (S3 is reachable with valid credentials).
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets why the app is offline, or null when it is online or has not been checked yet.
    /// </summary>
    public OfflineReason? Reason { get; set; }

    /// <summary>
    /// Gets or sets when the status was last checked, or null if it has not been.
    /// </summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    /// <summary>
    /// Creates a copy of the status.
    /// </summary>
    /// <returns>The copy.</returns>
    public ConnectivityStatus Clone() => (ConnectivityStatus)MemberwiseClone();
}
