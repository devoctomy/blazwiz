using Amazon.Runtime;
using Amazon.S3;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.S3;
using Moq;

namespace blazwiz.Core.IntTests.StepDefinitions;

[Binding]
public sealed class ReachabilityStepDefinitions(TestContext testContext)
{
    [Given("an S3 target with the configured credentials")]
    public void GivenAnS3TargetWithTheConfiguredCredentials()
    {
        testContext.ProbeTarget = new S3SyncTarget(testContext.S3Client!, testContext.AwsBucketName!, testContext.RootPrefix!);
    }

    [Given("an S3 target at an endpoint nothing is listening on")]
    public void GivenAnS3TargetAtABadEndpoint()
    {
        var client = new AmazonS3Client(
            new BasicAWSCredentials("AKIAEXAMPLE", "secret"),
            new AmazonS3Config { ServiceURL = "http://127.0.0.1:9", ForcePathStyle = true, MaxErrorRetry = 0 });
        testContext.ProbeTarget = new S3SyncTarget(client, "bucket", "user/");
    }

    [When("connectivity is checked")]
    public async Task WhenConnectivityIsChecked()
    {
        var offlineMode = new Mock<IOfflineModeStore>();
        var browser = new Mock<IBrowserNetworkStatus>();
        browser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
        var targets = new Mock<IS3SyncTargetProvider>();
        targets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(testContext.ProbeTarget);
        var user = new Mock<ISyncUserContext>();

        using var sut = new ConnectivityService(
            offlineMode.Object,
            browser.Object,
            targets.Object,
            new S3ReachabilityProbe(),
            Mock.Of<ISyncEventLog>(),
            user.Object,
            new SyncOptions(),
            TimeProvider.System);
        testContext.Connectivity = await sut.CheckAsync();
    }

    [Then("the app is online")]
    public void ThenTheAppIsOnline()
    {
        Assert.True(testContext.Connectivity!.IsOnline);
    }

    [Then("the app is offline because S3 is unreachable")]
    public void ThenTheAppIsOfflineBecauseS3IsUnreachable()
    {
        Assert.False(testContext.Connectivity!.IsOnline);
        Assert.Equal(OfflineReason.Unreachable, testContext.Connectivity.Reason);
    }
}
