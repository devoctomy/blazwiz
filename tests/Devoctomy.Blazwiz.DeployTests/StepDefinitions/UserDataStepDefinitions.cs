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
        var gone = await Assert.ThrowsAsync<AmazonS3Exception>(() => S3.GetObjectAsync(Bucket, OwnKey));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

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

    internal static async Task AssertDenied(Func<Task> call)
    {
        var refused = await Assert.ThrowsAsync<AmazonS3Exception>(call);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
