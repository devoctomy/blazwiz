using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.S3;

public class S3SyncRemoteStoreTests
{
    private const string Bucket = "data";
    private const string Root = "eu-west-2:abc/";
    private static readonly DateTime Uploaded = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly GlobSyncPathMatcher matcher = new("notes/**/*.md", true);

    public S3SyncRemoteStoreTests()
    {
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new S3SyncTarget(mockS3.Object, Bucket, Root));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new S3SyncRemoteStore(null!, matcher)).ParamName);
        Assert.Equal("matcher", Assert.Throws<ArgumentNullException>(() => new S3SyncRemoteStore(mockTargets.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenPagedListing_WhenListAsync_ThenAllPagesReadUnderPrefixAndFilteredByPattern()
    {
        // Arrange
        mockS3.SetupSequence(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [S3Object("notes/a.md"), S3Object("notes/image.png")],
                IsTruncated = true,
                NextContinuationToken = "next",
            })
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = [S3Object("notes/Work/"), S3Object("notes/Work/b.md")], IsTruncated = false });
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.ListAsync();

        // Assert
        Assert.Equal(["notes/a.md", "notes/Work/", "notes/Work/b.md"], result.Select(r => r.Key));
        Assert.Equal("\"etag-notes/a.md\"", result[0].ETag);
        Assert.Equal(new DateTimeOffset(Uploaded), result[0].LastModified);
        Assert.Equal(5, result[0].Size);
        mockS3.Verify(x => x.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.BucketName == Bucket && r.Prefix == Root + "notes/"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenObject_WhenGetAsync_ThenContentETagAndEditTimeRead()
    {
        // Arrange
        var response = new GetObjectResponse
        {
            ETag = "\"e\"",
            LastModified = Uploaded,
            ResponseStream = new MemoryStream([1, 2, 3]),
        };
        response.Metadata[S3SyncRemoteStore.ModifiedAtMetadata] = "2026-10-01T10:00:00.0000000+00:00";
        mockS3.Setup(x => x.GetObjectAsync(It.Is<GetObjectRequest>(r => r.BucketName == Bucket && r.Key == Root + "notes/a.md" && r.VersionId == null), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.GetAsync("notes/a.md");

        // Assert
        Assert.Equal([1, 2, 3], result!.Content);
        Assert.Equal("\"e\"", result.ETag);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), result.ModifiedAt);
        Assert.Equal(result.ModifiedAt, result.EditedAt);
    }

    [Fact]
    public async Task GivenMissingObject_WhenGetAsyncOrGetInfoAsync_ThenNull()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));
        mockS3.Setup(x => x.GetObjectMetadataAsync(Bucket, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act & Assert
        Assert.Null(await sut.GetAsync("notes/a.md"));
        Assert.Null(await sut.GetInfoAsync("notes/a.md"));
    }

    [Fact]
    public async Task GivenObjectWithoutMetadata_WhenGetInfoAsync_ThenEditTimeFallsBackToUploadTime()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectMetadataAsync(Bucket, Root + "notes/a.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { ETag = "\"e\"", LastModified = Uploaded, ContentLength = 4 });
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.GetInfoAsync("notes/a.md");

        // Assert
        Assert.Null(result!.ModifiedAt);
        Assert.Equal(new DateTimeOffset(Uploaded), result.EditedAt);
        Assert.Equal(4, result.Size);
    }

    [Fact]
    public async Task GivenConditions_WhenPutAsync_ThenConditionalUploadWithEditTimeAndNoChunkEncoding()
    {
        // Arrange
        PutObjectRequest? sent = null;
        byte[]? body = null;
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((r, _) =>
            {
                sent = r;
                using var copy = new MemoryStream();
                r.InputStream.CopyTo(copy);
                body = copy.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse { ETag = "\"new\"" });
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);
        var edited = new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromHours(1));

        // Act
        var etag = await sut.PutAsync("notes/a.md", [7, 8], edited, "\"old\"", false);

        // Assert
        Assert.Equal("\"new\"", etag);
        Assert.Equal(Bucket, sent!.BucketName);
        Assert.Equal(Root + "notes/a.md", sent.Key);
        Assert.Equal("\"old\"", sent.IfMatch);
        Assert.Null(sent.IfNoneMatch);
        Assert.False(sent.UseChunkEncoding);
        Assert.Equal("2026-10-01T10:00:00.0000000+00:00", sent.Metadata[S3SyncRemoteStore.ModifiedAtMetadata]);
        Assert.Equal([7, 8], body);
    }

    [Fact]
    public async Task GivenNewFile_WhenPutAsyncWithIfNoneMatch_ThenStarSent()
    {
        // Arrange
        PutObjectRequest? sent = null;
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PutObjectResponse { ETag = "\"new\"" });

        // Act
        await new S3SyncRemoteStore(mockTargets.Object, matcher).PutAsync("notes/a.md", [], DateTimeOffset.UnixEpoch, null, true);

        // Assert
        Assert.Equal("*", sent!.IfNoneMatch);
        Assert.Null(sent.IfMatch);
    }

    [Theory]
    [InlineData(HttpStatusCode.PreconditionFailed, null)]
    [InlineData(HttpStatusCode.Conflict, "ConditionalRequestConflict")]
    public async Task GivenObjectChanged_WhenPutAsyncOrDeleteAsync_ThenPreconditionFailed(HttpStatusCode status, string? errorCode)
    {
        // Arrange
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(status, errorCode));
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(status, errorCode));
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act & Assert
        await Assert.ThrowsAsync<SyncPreconditionFailedException>(() => sut.PutAsync("notes/a.md", [], DateTimeOffset.UnixEpoch, "\"e\"", false));
        await Assert.ThrowsAsync<SyncPreconditionFailedException>(() => sut.DeleteAsync("notes/a.md", "\"e\""));
    }

    [Fact]
    public async Task GivenETag_WhenDeleteAsync_ThenConditionalDelete()
    {
        // Arrange
        DeleteObjectRequest? sent = null;
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        await new S3SyncRemoteStore(mockTargets.Object, matcher).DeleteAsync("notes/a.md", "\"e\"");

        // Assert
        Assert.Equal(Root + "notes/a.md", sent!.Key);
        Assert.Equal("\"e\"", sent.IfMatch);
    }

    [Fact]
    public async Task GivenAlreadyGone_WhenDeleteAsync_ThenNothingThrown()
    {
        // Arrange
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));

        // Act & Assert
        await new S3SyncRemoteStore(mockTargets.Object, matcher).DeleteAsync("notes/a.md", "\"e\"");
    }

    [Fact]
    public async Task GivenForbidden_WhenCalled_ThenAccessDenied()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.Forbidden));

        // Act & Assert
        await Assert.ThrowsAsync<SyncAccessDeniedException>(() => new S3SyncRemoteStore(mockTargets.Object, matcher).ListAsync());
    }

    [Fact]
    public async Task GivenNetworkFailure_WhenCalled_ThenNetworkException()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("failed", new HttpRequestException()));
        mockS3.Setup(x => x.GetObjectMetadataAsync(Bucket, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act & Assert
        await Assert.ThrowsAsync<SyncNetworkException>(() => sut.ListAsync());
        await Assert.ThrowsAsync<SyncNetworkException>(() => sut.GetInfoAsync("notes/a.md"));
    }

    [Fact]
    public async Task GivenNoCredentials_WhenCalled_ThenAccessDenied()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((S3SyncTarget?)null);

        // Act & Assert
        await Assert.ThrowsAsync<SyncAccessDeniedException>(() => new S3SyncRemoteStore(mockTargets.Object, matcher).ListAsync());
    }

    [Theory]
    [InlineData(true, SyncErrorCodes.RemoteNoCredentials)]
    [InlineData(false, SyncErrorCodes.RemoteNotConfigured)]
    public async Task GivenNoTarget_WhenCalled_ThenAccessDeniedSaysWhetherSyncIsSetUp(bool isConfigured, string expectedCode)
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((S3SyncTarget?)null);
        mockTargets.SetupGet(x => x.IsConfigured).Returns(isConfigured);
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var ex = await Assert.ThrowsAsync<SyncAccessDeniedException>(() => sut.GetAsync("notes/a.md"));

        // Assert
        Assert.Equal(expectedCode, ex.Code);
        mockS3.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenAnObjectWithoutAnUploadTime_WhenListAsync_ThenItsUploadTimeIsTheEarliestPossible()
    {
        // Arrange
        var undated = S3Object("notes/a.md");
        undated.LastModified = null;
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = [undated], IsTruncated = false });
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.ListAsync();

        // Assert
        Assert.Equal(DateTimeOffset.MinValue, Assert.Single(result).LastModified);
    }

    [Fact]
    public async Task GivenCredentialServiceUnreachable_WhenCalled_ThenNetworkException()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());

        // Act & Assert
        await Assert.ThrowsAsync<SyncNetworkException>(() => new S3SyncRemoteStore(mockTargets.Object, matcher).ListAsync());
    }

    [Fact]
    public async Task GivenPagedVersions_WhenListVersionsAsync_ThenEveryVersionOfExactlyThatKeyIsListedNewestFirstWithoutDeleteMarkers()
    {
        // Arrange
        var older = Version("notes/a.md", "v1", Uploaded.AddHours(-2), isLatest: false);
        older.Size = null;
        mockS3.SetupSequence(x => x.ListVersionsAsync(It.IsAny<ListVersionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListVersionsResponse
            {
                Versions =
                [
                    Version("notes/a.md", "v3", Uploaded, isLatest: true),
                    Version("notes/a.md", "dm", Uploaded.AddHours(-1), isLatest: false, isDeleteMarker: true),
                ],
                IsTruncated = true,
                NextKeyMarker = Root + "notes/a.md",
                NextVersionIdMarker = "dm",
            })
            .ReturnsAsync(new ListVersionsResponse
            {
                Versions = [older, Version("notes/a.md.bak", "b1", Uploaded, isLatest: true)],
                IsTruncated = false,
            });
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.ListVersionsAsync("notes/a.md");

        // Assert
        Assert.Equal(["v3", "v1"], result.Select(v => v.VersionId));
        Assert.All(result, v => Assert.Equal("notes/a.md", v.Key));
        Assert.True(result[0].IsLatest);
        Assert.False(result[1].IsLatest);
        Assert.Equal("\"etag-v3\"", result[0].ETag);
        Assert.Equal(new DateTimeOffset(Uploaded), result[0].LastModified);
        Assert.Equal(5, result[0].Size);
        Assert.Equal(0, result[1].Size);
        mockS3.Verify(x => x.ListVersionsAsync(It.Is<ListVersionsRequest>(r => r.BucketName == Bucket && r.Prefix == Root + "notes/a.md"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenAListingOfVersionsThatIsCutShort_WhenListVersionsAsync_ThenTheNextPageStartsWhereItEnded()
    {
        // Arrange
        var markers = new List<(string? Key, string? VersionId)>();
        mockS3.Setup(x => x.ListVersionsAsync(It.IsAny<ListVersionsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ListVersionsRequest, CancellationToken>((r, _) => markers.Add((r.KeyMarker, r.VersionIdMarker)))
            .Returns(() => Task.FromResult(markers.Count == 1
                ? new ListVersionsResponse { Versions = [Version("notes/a.md", "v2", Uploaded, true)], IsTruncated = true, NextKeyMarker = Root + "notes/a.md", NextVersionIdMarker = "v2" }
                : new ListVersionsResponse { Versions = null, IsTruncated = null }));
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.ListVersionsAsync("notes/a.md");

        // Assert
        Assert.Equal("v2", Assert.Single(result).VersionId);
        Assert.Equal([(null, null), (Root + "notes/a.md", "v2")], markers);
    }

    [Fact]
    public async Task GivenAVersion_WhenGetVersionAsync_ThenThatVersionIsDownloaded()
    {
        // Arrange
        var response = new GetObjectResponse
        {
            ETag = "\"old\"",
            LastModified = Uploaded,
            ResponseStream = new MemoryStream([4, 5]),
        };
        mockS3.Setup(x => x.GetObjectAsync(It.Is<GetObjectRequest>(r => r.BucketName == Bucket && r.Key == Root + "notes/a.md" && r.VersionId == "v1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.GetVersionAsync("notes/a.md", "v1");

        // Assert
        Assert.Equal([4, 5], result!.Content);
        Assert.Equal("\"old\"", result.ETag);
        Assert.Equal("notes/a.md", result.Key);
        Assert.Equal(2, result.Size);
    }

    [Fact]
    public async Task GivenAVersionNoLongerThere_WhenGetVersionAsync_ThenNull()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act
        var result = await sut.GetVersionAsync("notes/a.md", "gone");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenForbiddenOrUnreachable_WhenListingOrGettingVersions_ThenAccessDeniedOrNetworkException()
    {
        // Arrange
        mockS3.Setup(x => x.ListVersionsAsync(It.IsAny<ListVersionsRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.Forbidden));
        mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act & Assert
        await Assert.ThrowsAsync<SyncAccessDeniedException>(() => sut.ListVersionsAsync("notes/a.md"));
        await Assert.ThrowsAsync<SyncNetworkException>(() => sut.GetVersionAsync("notes/a.md", "v1"));
    }

    [Fact]
    public async Task GivenNoKeyOrVersion_WhenListingOrGettingVersions_ThenThrowsArgumentException()
    {
        // Arrange
        var sut = new S3SyncRemoteStore(mockTargets.Object, matcher);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ListVersionsAsync(string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetVersionAsync(string.Empty, "v1"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetVersionAsync("notes/a.md", string.Empty));
        mockS3.VerifyNoOtherCalls();
    }

    private static S3ObjectVersion Version(string key, string versionId, DateTime uploaded, bool isLatest, bool isDeleteMarker = false) => new()
    {
        Key = Root + key,
        VersionId = versionId,
        ETag = $"\"etag-{versionId}\"",
        LastModified = uploaded,
        Size = 5,
        IsLatest = isLatest,
        IsDeleteMarker = isDeleteMarker,
    };

    private static S3Object S3Object(string key) => new()
    {
        Key = Root + key,
        ETag = $"\"etag-{key}\"",
        LastModified = Uploaded,
        Size = 5,
    };

    private static AmazonS3Exception S3Error(HttpStatusCode status, string? errorCode = null) =>
        new("error", ErrorType.Sender, errorCode ?? status.ToString(), "request", status);
}
