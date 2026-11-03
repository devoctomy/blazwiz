using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Reqnroll.UnitTestProvider;

namespace Devoctomy.Blazwiz.Sync.IntTests.Hooks;

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
    public const string BucketNameVariable = "BLAZWIZ_INTTEST_AWS_BUCKETNAME";

    [BeforeScenario("aws")]
    public void LoadAwsCredentials()
    {
        testContext.AwsAccessKey = Environment.GetEnvironmentVariable(AccessKeyVariable);
        testContext.AwsSecretAccessKey = Environment.GetEnvironmentVariable(SecretAccessKeyVariable);
        testContext.AwsRegion = Environment.GetEnvironmentVariable(RegionVariable);
        testContext.AwsBucketName = Environment.GetEnvironmentVariable(BucketNameVariable);

        if (string.IsNullOrWhiteSpace(testContext.AwsAccessKey) ||
            string.IsNullOrWhiteSpace(testContext.AwsSecretAccessKey) ||
            string.IsNullOrWhiteSpace(testContext.AwsBucketName) ||
            string.IsNullOrWhiteSpace(testContext.AwsRegion))
        {
            var required = $"{AccessKeyVariable}, {SecretAccessKeyVariable}, {RegionVariable}";
            required += $", {BucketNameVariable}";
            unitTestRuntimeProvider.TestIgnore($"AWS not configured. Set {required} to run @aws scenarios.");
            return;
        }

        testContext.S3Client = new AmazonS3Client(
            new BasicAWSCredentials(testContext.AwsAccessKey, testContext.AwsSecretAccessKey),
            RegionEndpoint.GetBySystemName(testContext.AwsRegion));
        testContext.RootPrefix = $"blazwiz-inttests/{Guid.NewGuid():N}/";
    }

    [AfterScenario("aws")]
    public async Task DeleteScenarioObjects()
    {
        if (testContext.S3Client == null || testContext.RootPrefix == null)
        {
            return;
        }

        var request = new ListObjectsV2Request { BucketName = testContext.AwsBucketName, Prefix = testContext.RootPrefix };
        do
        {
            var response = await testContext.S3Client.ListObjectsV2Async(request);
            foreach (var item in response.S3Objects ?? [])
            {
                await testContext.S3Client.DeleteObjectAsync(testContext.AwsBucketName, item.Key);
            }

            request.ContinuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (request.ContinuationToken != null);

        testContext.S3Client.Dispose();
    }
}
