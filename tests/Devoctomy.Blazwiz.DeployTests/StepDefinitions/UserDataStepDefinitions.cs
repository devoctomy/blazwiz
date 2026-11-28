using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace Devoctomy.Blazwiz.DeployTests.StepDefinitions;

[Binding]
public sealed class UserDataStepDefinitions(DeployTestContext context) : IDisposable
{
    private const string OtherUser = "someone-else";

    private AmazonS3Client? s3;

    private AmazonS3Client S3 => s3 ??= context.RequiredUser.CreateS3Client();

    private string Bucket => context.RequiredPortal.DataBucketName;

    private string OwnKey => $"{context.RequiredUser.IdentityId}/deploytests-{context.RunId}.txt";

    private string Content => $"deploy tests {context.RunId}";

    private string NewContent => $"deploy tests {context.RunId}, changed";

    [When("they write a test file in their own prefix")]
    public Task WhenTheyWriteATestFile() =>
        S3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = OwnKey, ContentBody = Content });

    [Then("they can read it back")]
    public async Task ThenTheyCanReadItBack()
    {
        using var response = await S3.GetObjectAsync(Bucket, OwnKey);
        using var reader = new StreamReader(response.ResponseStream);
        Assert.Equal(Content, await reader.ReadToEndAsync());
    }

    [Then("they can list it")]
    public async Task ThenTheyCanListIt()
    {
        var listing = await S3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = $"{context.RequiredUser.IdentityId}/deploytests-" });
        Assert.Contains(listing.S3Objects ?? [], o => o.Key == OwnKey);
    }

    [Then("they can delete it")]
    public async Task ThenTheyCanDeleteIt()
    {
        await S3.DeleteObjectAsync(Bucket, OwnKey);
        var gone = await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => S3.GetObjectAsync(Bucket, OwnKey));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [When("they write it again with new content")]
    public Task WhenTheyWriteItAgain() =>
        S3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = OwnKey, ContentBody = NewContent });

    // The test file's key is the same in every scenario of a run, and versions are kept after a delete, so other
    // scenarios' versions of it may be listed too: every earlier one holds the first content.
    [Then("they can list both versions of it")]
    public async Task ThenTheyCanListBothVersionsOfIt()
    {
        var versions = await ListOwnVersionsAsync();
        Assert.True(versions.Count >= 2);
        Assert.Single(versions, v => v.IsLatest == true);
    }

    [Then("they can read an earlier version of it")]
    public async Task ThenTheyCanReadAnEarlierVersionOfIt()
    {
        var earlier = await EarlierVersionIdAsync();
        using var response = await S3.GetObjectAsync(new GetObjectRequest { BucketName = Bucket, Key = OwnKey, VersionId = earlier });
        using var reader = new StreamReader(response.ResponseStream);
        Assert.Equal(Content, await reader.ReadToEndAsync());
    }

    [Then("they cannot delete a version of it")]
    public async Task ThenTheyCannotDeleteAVersionOfIt()
    {
        var earlier = await EarlierVersionIdAsync();
        await AssertDenied(() => S3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = OwnKey, VersionId = earlier }));
    }

    [Then("they cannot list the versions in another user's prefix")]
    public Task ThenTheyCannotListTheVersionsInAnotherUsersPrefix() => AssertDenied(() =>
        S3.ListVersionsAsync(new ListVersionsRequest { BucketName = Bucket, Prefix = $"{OtherUser}/" }));

    [Then("they cannot read a version from another user's prefix")]
    public Task ThenTheyCannotReadAVersionFromAnotherUsersPrefix() => AssertDenied(() =>
        S3.GetObjectAsync(new GetObjectRequest { BucketName = Bucket, Key = $"{OtherUser}/deploytests-{context.RunId}.txt", VersionId = "null" }));

    [Then("they cannot list the versions in the whole bucket")]
    public Task ThenTheyCannotListTheVersionsInTheWholeBucket() => AssertDenied(() =>
        S3.ListVersionsAsync(new ListVersionsRequest { BucketName = Bucket }));

    [Then("they cannot write in another user's prefix")]
    public Task ThenTheyCannotWriteInAnotherUsersPrefix() => AssertDenied(() =>
        S3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = $"{OtherUser}/deploytests-{context.RunId}.txt", ContentBody = Content }));

    [Then("they cannot read from another user's prefix")]
    public Task ThenTheyCannotReadFromAnotherUsersPrefix() => AssertDenied(() =>
        S3.GetObjectAsync(Bucket, $"{OtherUser}/deploytests-{context.RunId}.txt"));

    [Then("they cannot list another user's prefix")]
    public Task ThenTheyCannotListAnotherUsersPrefix() => AssertDenied(() =>
        S3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = $"{OtherUser}/" }));

    [Then("they cannot list the whole bucket")]
    public Task ThenTheyCannotListTheWholeBucket() => AssertDenied(() =>
        S3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket }));

    [AfterScenario]
    public async Task RemoveTheTestFile()
    {
        if (s3 == null)
        {
            return;
        }

        try
        {
            await s3.DeleteObjectAsync(Bucket, OwnKey);
        }
        catch (AmazonS3Exception)
        {
            // Best effort: the test file may never have been written.
        }
    }

    public void Dispose() => s3?.Dispose();

    private async Task<List<S3ObjectVersion>> ListOwnVersionsAsync()
    {
        var listing = await S3.ListVersionsAsync(new ListVersionsRequest { BucketName = Bucket, Prefix = OwnKey });
        return (listing.Versions ?? []).Where(v => v.Key == OwnKey && v.IsDeleteMarker != true).ToList();
    }

    private async Task<string> EarlierVersionIdAsync() =>
        (await ListOwnVersionsAsync()).First(v => v.IsLatest != true).VersionId;

    internal static async Task AssertDenied(Func<Task> call)
    {
        var refused = await Assert.ThrowsAnyAsync<AmazonS3Exception>(call);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
