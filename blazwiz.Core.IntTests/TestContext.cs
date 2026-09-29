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

    public DateTimeOffset? CurrentTime { get; set; }

    public string? UserName { get; set; }

    public string? Greeting { get; set; }
}
