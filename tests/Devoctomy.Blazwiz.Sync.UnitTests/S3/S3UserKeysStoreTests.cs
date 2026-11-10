using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.S3;

public class S3UserKeysStoreTests
{
    private const string Bucket = "data";
    private const string Root = "eu-west-2:abc/";
    private const string ScopeId = "5f0c6b5e-3c1e-4f43-9c7e-0b8d5b0f2a11";

    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly Mock<IOfflineModeService> mockOffline = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero));
    private readonly E2eKeyScope scope = new(ScopeId);

    public S3UserKeysStoreTests()
    {
        mockTargets.Setup(x => x.IsConfigured).Returns(true);
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new S3SyncTarget(mockS3.Object, Bucket, Root));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new S3UserKeysStore(null!, mockOffline.Object, clock)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new S3UserKeysStore(mockTargets.Object, null!, clock)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new S3UserKeysStore(mockTargets.Object, mockOffline.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAKeysDocument_WhenGot_ThenReadFromTheKeysFolderWithItsETag()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(Bucket, $"{Root}keys/{ScopeId}", It.IsAny<CancellationToken>())).ReturnsAsync(Object("{\"keys\":1}", "\"e1\""));

        // Act
        var stored = await CreateSut().GetAsync(scope);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal("{\"keys\":1}", stored.Value);
        Assert.Equal("\"e1\"", stored.ETag);
    }

    [Fact]
    public async Task GivenTheIndex_WhenGot_ThenReadFromKeysIndex()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(Bucket, $"{Root}keys/index", It.IsAny<CancellationToken>())).ReturnsAsync(Object("{\"index\":1}", "\"e2\""));

        // Act
        var stored = await CreateSut().GetIndexAsync();

        // Assert
        Assert.Equal("{\"index\":1}", stored!.Value);
        Assert.Equal("\"e2\"", stored.ETag);
    }

    [Fact]
    public async Task GivenNoDocument_WhenGot_ThenNull()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(Bucket, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetAsync(scope));
        Assert.Null(await sut.GetIndexAsync());
    }

    [Fact]
    public async Task GivenAVersion_WhenSet_ThenWrittenOnlyIfItStillMatches()
    {
        // Arrange
        var sent = CapturePut("\"e2\"");

        // Act
        var etag = await CreateSut().SetAsync(scope, "keys", "\"e1\"");

        // Assert
        Assert.Equal("\"e2\"", etag);
        Assert.Equal(Bucket, sent()!.BucketName);
        Assert.Equal($"{Root}keys/{ScopeId}", sent()!.Key);
        Assert.Equal("keys", sent()!.ContentBody);
        Assert.Equal("application/json", sent()!.ContentType);
        Assert.Equal("\"e1\"", sent()!.IfMatch);
        Assert.Null(sent()!.IfNoneMatch);
        Assert.False(sent()!.UseChunkEncoding);
        Assert.Equal("2026-11-10T09:30:00.0000000+00:00", sent()!.Metadata[S3SyncRemoteStore.ModifiedAtMetadata]);
    }

    [Fact]
    public async Task GivenNoKeys_WhenTryCreate_ThenWrittenOnlyIfNoneExist()
    {
        // Arrange
        var sent = CapturePut("\"e1\"");

        // Act
        var etag = await CreateSut().TryCreateAsync(scope, "keys");

        // Assert
        Assert.Equal("\"e1\"", etag);
        Assert.Equal("*", sent()!.IfNoneMatch);
        Assert.Null(sent()!.IfMatch);
    }

    [Fact]
    public async Task GivenExistingKeys_WhenTryCreate_ThenNull()
    {
        // Arrange
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.PreconditionFailed));

        // Act
        var etag = await CreateSut().TryCreateAsync(scope, "keys");

        // Assert
        Assert.Null(etag);
    }

    [Fact]
    public async Task GivenNoIndexVersion_WhenTheIndexIsSet_ThenWrittenOnlyIfThereIsNone()
    {
        // Arrange
        var sent = CapturePut("\"e1\"");

        // Act
        var etag = await CreateSut().SetIndexAsync("index", null);

        // Assert
        Assert.Equal("\"e1\"", etag);
        Assert.Equal($"{Root}keys/index", sent()!.Key);
        Assert.Equal("*", sent()!.IfNoneMatch);
        Assert.Null(sent()!.IfMatch);
    }

    [Fact]
    public async Task GivenAnIndexVersion_WhenTheIndexIsSet_ThenWrittenOnlyIfItStillMatches()
    {
        // Arrange
        var sent = CapturePut("\"e2\"");

        // Act
        await CreateSut().SetIndexAsync("index", "\"e1\"");

        // Assert
        Assert.Equal("\"e1\"", sent()!.IfMatch);
        Assert.Null(sent()!.IfNoneMatch);
    }

    [Theory]
    [InlineData(HttpStatusCode.PreconditionFailed, null)]
    [InlineData(HttpStatusCode.Conflict, "ConditionalRequestConflict")]
    [InlineData(HttpStatusCode.NotFound, "NoSuchKey")]
    public async Task GivenTheDocumentChangedOrWentSinceRead_WhenSet_ThenThrowsE2eDocumentChangedException(HttpStatusCode status, string? errorCode)
    {
        // Arrange
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(status, errorCode));
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetAsync(scope, "keys", "\"e1\""));
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetIndexAsync("index", "\"e1\""));
    }

    [Fact]
    public async Task GivenAnIndexCreatedSinceRead_WhenSetWithoutAVersion_ThenThrowsE2eDocumentChangedException()
    {
        // Arrange
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.PreconditionFailed));

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => CreateSut().SetIndexAsync("index", null));
    }

    [Fact]
    public async Task GivenKeys_WhenDeleted_ThenTheObjectIsDeletedUnconditionally()
    {
        // Arrange
        DeleteObjectRequest? sent = null;
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        await CreateSut().DeleteAsync(scope);

        // Assert
        Assert.Equal(Bucket, sent!.BucketName);
        Assert.Equal($"{Root}keys/{ScopeId}", sent.Key);
        Assert.Null(sent.IfMatch);
        Assert.Null(sent.VersionId);
    }

    [Fact]
    public async Task GivenNoKeys_WhenDeleted_ThenNothingThrown()
    {
        // Arrange
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(S3Error(HttpStatusCode.NotFound));

        // Act
        var exception = await Record.ExceptionAsync(() => CreateSut().DeleteAsync(scope));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenUsed_ThenThrowsHttpRequestExceptionWithoutCallingS3()
    {
        // Arrange
        mockOffline.Setup(x => x.IsOffline).Returns(true);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync(scope));
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetIndexAsync());
        mockTargets.Verify(x => x.GetTargetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoTarget_WhenUsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((S3SyncTarget?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAsync(scope));
    }

    [Fact]
    public async Task GivenTheCredentialsServiceUnreachable_WhenUsed_ThenThrowsHttpRequestException()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSut().GetIndexAsync());
    }

    [Fact]
    public async Task GivenS3Unreachable_WhenUsed_ThenThrowsHttpRequestException()
    {
        // Arrange
        mockS3.Setup(x => x.GetObjectAsync(Bucket, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("unreachable", new HttpRequestException("down")));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSut().GetAsync(scope));
    }

    [Fact]
    public async Task GivenNullArguments_WhenUsed_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(scope, null!, "\"e\""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.SetAsync(scope, "keys", string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryCreateAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetIndexAsync(null!, null));
    }

    private static GetObjectResponse Object(string content, string etag) => new()
    {
        ETag = etag,
        ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
    };

    private static AmazonS3Exception S3Error(HttpStatusCode status, string? errorCode = null) =>
        new("error", ErrorType.Sender, errorCode ?? status.ToString(), "request", status);

    private Func<PutObjectRequest?> CapturePut(string etag)
    {
        PutObjectRequest? sent = null;
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PutObjectResponse { ETag = etag });
        return () => sent;
    }

    private S3UserKeysStore CreateSut() => new(mockTargets.Object, mockOffline.Object, clock);
}
