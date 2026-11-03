namespace Devoctomy.Blazwiz.Sync.Connectivity;

/// <summary>
/// Why sync cannot reach the cloud, in the order the checks run.
/// </summary>
public enum OfflineReason
{
    /// <summary>
    /// The app is in offline mode, so nothing calls AWS.
    /// </summary>
    OfflineMode,

    /// <summary>
    /// The user turned sync off.
    /// </summary>
    SyncDisabled,

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
