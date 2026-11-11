using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.S3;

public class S3SyncScratchStoreTests
{
    private const string Bucket = "data";
    private const string Root = "eu-west-2:abc/";

    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

    public S3SyncScratchStoreTests()
    {
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new S3SyncTarget(mockS3.Object, Bucket, Root));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new S3SyncScratchStore(null!, clock)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new S3SyncScratchStore(mockTargets.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAnObject_WhenPutAndDeleted_ThenItIsInTheScratchFolderOfTheUsersPrefix()
    {
        // Arrange
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse { ETag = "\"e\"" });
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteObjectResponse());
        var sut = new S3SyncScratchStore(mockTargets.Object, clock);

        // Act
        await sut.PutAsync("timing", [1, 2, 3]);
        await sut.DeleteAsync("timing");

        // Assert
        mockS3.Verify(x => x.PutObjectAsync(It.Is<PutObjectRequest>(r => r.BucketName == Bucket && r.Key == Root + "scratch/timing"), It.IsAny<CancellationToken>()), Times.Once);
        mockS3.Verify(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r => r.BucketName == Bucket && r.Key == Root + "scratch/timing"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    public async Task GivenANameThatIsNotOneObjectInTheScratchFolder_WhenPut_ThenThrows(string name)
    {
        // Arrange
        var sut = new S3SyncScratchStore(mockTargets.Object, clock);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.PutAsync(name, [1]));
    }

    [Fact]
    public async Task GivenNoSyncTarget_WhenPut_ThenThrowsAccessDenied()
    {
        // Arrange
        mockTargets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((S3SyncTarget?)null);
        var sut = new S3SyncScratchStore(mockTargets.Object, clock);

        // Act & Assert
        await Assert.ThrowsAsync<SyncAccessDeniedException>(() => sut.PutAsync("timing", [1]));
    }
}
