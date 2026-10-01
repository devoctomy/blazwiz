namespace blazwiz.Core.Sync.Configuration;

/// <summary>
/// How often the connectivity service checks the connection.
/// </summary>
public class ConnectivityTimings
{
    /// <summary>
    /// Gets or sets how long a check is reused for. 30 seconds by default.
    /// </summary>
    public TimeSpan CacheFor { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long to wait for S3 to answer the reachability probe. 5 seconds by default.
    /// </summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the delays between rechecks while offline (other than offline mode). The last one repeats.
    /// 30 seconds, 1 minute, 2 minutes, then every 5 minutes by default.
    /// </summary>
    public IReadOnlyList<TimeSpan> RecheckBackoff { get; set; } =
        [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];
}
