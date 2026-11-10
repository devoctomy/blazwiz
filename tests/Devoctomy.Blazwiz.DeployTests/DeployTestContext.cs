using Devoctomy.Blazwiz.DeployTests.Support;

namespace Devoctomy.Blazwiz.DeployTests;

/// <summary>
/// Shared state for one scenario, injected by Reqnroll into every step definition class that asks for it.
/// </summary>
public sealed class DeployTestContext : IDisposable
{
    /// <summary>
    /// Gets one HTTP client for the run.
    /// </summary>
    public static HttpClient Http { get; } = new HttpClient();

    public DeployTestSettings? Settings { get; set; }

    public PortalSettings? Portal { get; set; }

    public TestUser? User { get; set; }

    /// <summary>
    /// Gets a value for each scenario, so what one run writes cannot meet another's.
    /// </summary>
    public string RunId { get; } = Guid.NewGuid().ToString("N");

    public DeployTestSettings RequiredSettings => Settings ?? throw new InvalidOperationException("No deploy test settings.");

    public PortalSettings RequiredPortal => Portal ?? throw new InvalidOperationException("The portal's settings were not read.");

    public TestUser RequiredUser => User ?? throw new InvalidOperationException("The test user is not signed in.");

    public void Dispose() => User?.Dispose();
}
