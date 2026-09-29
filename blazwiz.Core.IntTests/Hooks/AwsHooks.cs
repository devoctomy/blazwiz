using Reqnroll.UnitTestProvider;

namespace blazwiz.Core.IntTests.Hooks;

/// <summary>
/// Loads AWS credentials from environment variables for any scenario tagged <c>@aws</c>.
/// If the credentials are not configured the scenario is skipped rather than failed, so the
/// suite can always be run locally without an AWS account.
/// </summary>
[Binding]
public sealed class AwsHooks(TestContext testContext, IUnitTestRuntimeProvider unitTestRuntimeProvider)
{
    public const string AccessKeyVariable = "BLAZWIZ_INTTEST_AWS_ACCESSKEY";
    public const string SecretAccessKeyVariable = "BLAZWIZ_INTTEST_AWS_SECRETACCESSKEY";
    public const string RegionVariable = "BLAZWIZ_INTTEST_AWS_REGION";

    [BeforeScenario("aws")]
    public void LoadAwsCredentials()
    {
        testContext.AwsAccessKey = Environment.GetEnvironmentVariable(AccessKeyVariable);
        testContext.AwsSecretAccessKey = Environment.GetEnvironmentVariable(SecretAccessKeyVariable);
        testContext.AwsRegion = Environment.GetEnvironmentVariable(RegionVariable);

        if (string.IsNullOrWhiteSpace(testContext.AwsAccessKey) ||
            string.IsNullOrWhiteSpace(testContext.AwsSecretAccessKey) ||
            string.IsNullOrWhiteSpace(testContext.AwsRegion))
        {
            unitTestRuntimeProvider.TestIgnore(
                $"AWS credentials not configured. Set {AccessKeyVariable}, {SecretAccessKeyVariable} and {RegionVariable} to run @aws scenarios.");
        }
    }
}
