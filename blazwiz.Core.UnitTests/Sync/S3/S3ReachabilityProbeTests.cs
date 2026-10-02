using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.S3;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.S3;

public class S3ReachabilityProbeTests
{
    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly S3SyncTarget target;

    public S3ReachabilityProbeTests()
    {
        target = new S3SyncTarget(mockS3.Object, "data", "id/");
    }

    [Fact]
    public async Task GivenS3Answers_WhenIsReachableAsync_ThenListsOneKeyOfOwnPrefix()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());

        // Act
        var result = await new S3ReachabilityProbe().IsReachableAsync(target, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(result);
        mockS3.Verify(x => x.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.BucketName == "data" && r.Prefix == "id/" && r.MaxKeys == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenS3RefusesCredentials_WhenIsReachableAsync_ThenFalse()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("denied", ErrorType.Sender, "AccessDenied", "r", HttpStatusCode.Forbidden));

        // Act & Assert
        Assert.False(await new S3ReachabilityProbe().IsReachableAsync(target, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task GivenNoNetwork_WhenIsReachableAsync_ThenFalse()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());

        // Act & Assert
        Assert.False(await new S3ReachabilityProbe().IsReachableAsync(target, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task GivenS3TooSlow_WhenIsReachableAsync_ThenFalseAfterTimeout()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .Returns<ListObjectsV2Request, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new ListObjectsV2Response();
            });

        // Act & Assert
        Assert.False(await new S3ReachabilityProbe().IsReachableAsync(target, TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public async Task GivenCallerCancels_WhenIsReachableAsync_ThenCancellationPropagates()
    {
        // Arrange
        mockS3.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .Returns<ListObjectsV2Request, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new ListObjectsV2Response();
            });
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new S3ReachabilityProbe().IsReachableAsync(target, TimeSpan.FromMinutes(1), source.Token));
    }

    [Fact]
    public async Task GivenNullTarget_WhenIsReachableAsync_ThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => new S3ReachabilityProbe().IsReachableAsync(null!, TimeSpan.FromSeconds(1)));
    }
}
