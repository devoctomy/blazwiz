namespace blazwiz.Core.Sync.Connectivity;

/// <summary>
/// Why the app is offline, in the order the checks run.
/// </summary>
public enum OfflineReason
{
    /// <summary>
    /// The user turned on offline mode.
    /// </summary>
    ForcedOffline,

    /// <summary>
    /// The browser reports no network.
    /// </summary>
    NoNetwork,

    /// <summary>
    /// There are no AWS credentials (e.g. the placeholder sign-in, or the identity pool exchange failed).
    /// </summary>
    NoCredentials,

    /// <summary>
    /// S3 could not be reached.
    /// </summary>
    Unreachable,
}
