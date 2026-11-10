using Devoctomy.Blazwiz.DeployTests.Support;
using Reqnroll.UnitTestProvider;

namespace Devoctomy.Blazwiz.DeployTests.Hooks;

/// <summary>
/// Reads the settings before each scenario, and skips it (rather than failing) when the deploy tests were not given an
/// environment, when it needs the events API and the stack has none, or when it signs out everywhere and that was not
/// asked for.
/// </summary>
[Binding]
public sealed class DeployHooks(DeployTestContext context, ScenarioContext scenario, IUnitTestRuntimeProvider runtime)
{
    private static PortalSettings? portal;

    [BeforeScenario(Order = 0)]
    public async Task LoadSettings()
    {
        context.Settings = DeployTestSettings.FromEnvironment();
        if (context.Settings == null)
        {
            runtime.TestIgnore($"No deployed environment. Set {DeployTestSettings.PortalUrlVariable}, {DeployTestSettings.EmailVariable} and {DeployTestSettings.PasswordVariable} to run the deploy tests.");
            return;
        }

        portal ??= await PortalSettings.FetchAsync(DeployTestContext.Http, context.Settings.PortalUrl);
        context.Portal = portal;
        var tags = scenario.ScenarioInfo.CombinedTags;
        if (tags.Contains("events") && portal.EventsHttpEndpoint == null)
        {
            runtime.TestIgnore("The stack has no user events API (Events=disabled).");
        }

        if (tags.Contains("signouteverywhere") && !context.Settings.SignOutEverywhere)
        {
            runtime.TestIgnore($"Signs the test user out on every device: set {DeployTestSettings.SignOutEverywhereVariable}=true to run it.");
        }
    }
}
