//#if (IncludeNotes)
using Amazon.S3;
using blazwiz.Core.IntTests.Support;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Models;
//#endif

namespace blazwiz.Core.IntTests;

/// <summary>
/// Shared state for a single scenario. Reqnroll injects one instance per scenario into
/// every step definition class that asks for it, so steps in different classes can share data.
/// </summary>
public class TestContext
{
    public string? AwsAccessKey { get; set; }

    public string? AwsSecretAccessKey { get; set; }

    public string? AwsRegion { get; set; }
//#if (IncludeNotes)

    public string? AwsBucketName { get; set; }
//#endif

    public DateTimeOffset? CurrentTime { get; set; }

    public string? UserName { get; set; }

    public string? Greeting { get; set; }
//#if (IncludeNotes)

    /// <summary>
    /// Gets or sets the S3 client for the scenario, created from the configured credentials.
    /// </summary>
    public IAmazonS3? S3Client { get; set; }

    /// <summary>
    /// Gets or sets the scenario's own root prefix in the bucket (stands in for the user's identity id).
    /// </summary>
    public string? RootPrefix { get; set; }

    /// <summary>
    /// Gets the simulated devices, by name.
    /// </summary>
    public Dictionary<string, SimulatedDevice> Devices { get; } = [];

    /// <summary>
    /// Gets the results of each device's last sync, by name.
    /// </summary>
    public Dictionary<string, SyncResult> LastResults { get; } = [];

    /// <summary>
    /// Gets or sets the S3 target used by the reachability scenarios.
    /// </summary>
    public S3SyncTarget? ProbeTarget { get; set; }

    /// <summary>
    /// Gets or sets the connectivity status from the reachability scenarios.
    /// </summary>
    public ConnectivityStatus? Connectivity { get; set; }
//#endif
}
