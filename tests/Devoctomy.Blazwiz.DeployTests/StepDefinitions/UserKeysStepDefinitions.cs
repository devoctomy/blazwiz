using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Moq;

namespace Devoctomy.Blazwiz.DeployTests.StepDefinitions;

/// <summary>
/// The user's keys documents, written through the portal's own store (<see cref="S3UserKeysStore"/>) with the user's
/// credentials, under a made-up profile id.
/// </summary>
[Binding]
public sealed class UserKeysStepDefinitions(DeployTestContext context) : IDisposable
{
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));
    private AmazonS3Client? s3;
    private S3UserKeysStore? store;
    private string? firstCreation;
    private string? secondCreation;
    private string? createdETag;
    private string? createdVersionId;
    private Exception? replacementRefusal;
    private bool deleteMarker;

    private AmazonS3Client S3 => s3 ??= context.RequiredUser.CreateS3Client();

    private string Bucket => context.RequiredPortal.DataBucketName;

    private string KeysKey => $"{context.RequiredUser.IdentityId}/keys/{scope.Id}";

    private string Document(string version) => $"{{\"deployTest\":\"{context.RunId}\",\"version\":\"{version}\"}}";

    private S3UserKeysStore Store
    {
        get
        {
            if (store == null)
            {
                var targets = new Mock<IS3SyncTargetProvider>();
                targets.Setup(x => x.IsConfigured).Returns(true);
                targets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new S3SyncTarget(S3, Bucket, context.RequiredUser.IdentityId + "/"));
                store = new S3UserKeysStore(targets.Object, Mock.Of<IOfflineModeService>(), TimeProvider.System);
            }

            return store;
        }
    }

    [When("they create a keys document")]
    public async Task WhenTheyCreateAKeysDocument()
    {
        firstCreation = await Store.TryCreateAsync(scope, Document("1"));
    }

    [When("they create the same keys document again")]
    public async Task WhenTheyCreateItAgain()
    {
        secondCreation = await Store.TryCreateAsync(scope, Document("2"));
    }

    [Then("the second creation is refused")]
    public async Task ThenTheSecondCreationIsRefused()
    {
        Assert.NotNull(firstCreation);
        Assert.Null(secondCreation);
        Assert.Equal(Document("1"), (await Store.GetAsync(scope))?.Value);
    }

    [Given("they have created a keys document")]
    public async Task GivenTheyHaveCreatedAKeysDocument()
    {
        createdETag = await Store.TryCreateAsync(scope, Document("1"));
        Assert.NotNull(createdETag);
        createdVersionId = (await S3.GetObjectMetadataAsync(Bucket, KeysKey)).VersionId;
        Assert.False(string.IsNullOrEmpty(createdVersionId), "The data bucket is not versioned.");
    }

    [When("they replace it at the version they read")]
    public async Task WhenTheyReplaceItAtTheVersionTheyRead()
    {
        var replaced = await Store.SetAsync(scope, Document("2"), createdETag!);
        Assert.NotEqual(createdETag, replaced);
    }

    [When("they replace it again at that same, older version")]
    public async Task WhenTheyReplaceItAgainAtTheOlderVersion()
    {
        replacementRefusal = await Record.ExceptionAsync(() => Store.SetAsync(scope, Document("3"), createdETag!));
    }

    [Then("the second replacement is refused as changed")]
    public async Task ThenTheSecondReplacementIsRefused()
    {
        Assert.IsType<E2eDocumentChangedException>(replacementRefusal);
        Assert.Equal(Document("2"), (await Store.GetAsync(scope))?.Value);
    }

    [When("they delete it")]
    public async Task WhenTheyDeleteIt()
    {
        var response = await S3.DeleteObjectAsync(Bucket, KeysKey);
        deleteMarker = string.Equals(response.DeleteMarker?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    [Then("the bucket kept it behind a delete marker")]
    public async Task ThenTheBucketKeptItBehindADeleteMarker()
    {
        Assert.True(deleteMarker, "Expected the delete to add a delete marker.");
        Assert.Null(await Store.GetAsync(scope));
    }

    [Then("they cannot delete the version they created")]
    public Task ThenTheyCannotDeleteTheVersion() => UserDataStepDefinitions.AssertDenied(() =>
        S3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = KeysKey, VersionId = createdVersionId }));

    [Then("they cannot create another user's keys document")]
    public Task ThenTheyCannotCreateAnotherUsersKeys() => UserDataStepDefinitions.AssertDenied(() =>
        S3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = $"someone-else/keys/{scope.Id}", ContentBody = Document("1"), IfNoneMatch = "*" }));

    [Then("they cannot read another user's keys document")]
    public Task ThenTheyCannotReadAnotherUsersKeys() => UserDataStepDefinitions.AssertDenied(() =>
        S3.GetObjectAsync(Bucket, "someone-else/keys/index"));

    [AfterScenario]
    public async Task RemoveTheTestDocument()
    {
        if (store == null)
        {
            return;
        }

        try
        {
            // Only a delete marker: the user cannot delete versions. The lifecycle rule expires them.
            await store.DeleteAsync(scope);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or HttpRequestException)
        {
            // Best effort.
        }
    }

    public void Dispose() => s3?.Dispose();
}
