namespace blazwiz.Core.Sync.Connectivity;

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
    /// Describes the reason for the user, e.g. "Offline: S3 unreachable".
    /// </summary>
    /// <returns>The description.</returns>
    public string Describe()
    {
        if (IsOnline)
        {
            return "Online";
        }

        return Reason switch
        {
            OfflineReason.OfflineMode => "Offline: offline mode",
            OfflineReason.SyncDisabled => "Sync turned off",
            OfflineReason.NoNetwork => "Offline: no network",
            OfflineReason.NoCredentials => "Offline: no credentials",
            OfflineReason.Unreachable => "Offline: S3 unreachable",
            _ => "Checking connection",
        };
    }

    /// <summary>
    /// Creates a copy of the status.
    /// </summary>
    /// <returns>The copy.</returns>
    public ConnectivityStatus Clone() => (ConnectivityStatus)MemberwiseClone();
}
