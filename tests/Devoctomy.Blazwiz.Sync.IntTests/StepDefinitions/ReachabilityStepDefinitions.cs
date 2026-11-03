using Amazon.Runtime;
using Amazon.S3;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Moq;

namespace Devoctomy.Blazwiz.Sync.IntTests.StepDefinitions;

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
        var syncEnabled = new Mock<ISyncEnabledStore>();
        syncEnabled.Setup(x => x.GetAsync()).ReturnsAsync(true);
        var browser = new Mock<IBrowserNetworkStatus>();
        browser.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
        var targets = new Mock<IS3SyncTargetProvider>();
        targets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(testContext.ProbeTarget);
        var user = new Mock<ISyncUserContext>();

        using var sut = new ConnectivityService(
            new OfflineModeService(),
            syncEnabled.Object,
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
