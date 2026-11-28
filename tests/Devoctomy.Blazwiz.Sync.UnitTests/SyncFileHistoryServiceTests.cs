using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.UnitTests.Fakes;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class SyncFileHistoryServiceTests
{
    private const string Profile = "notes";
    private const string Key = "notes/plan.md";

    private readonly FakeSyncRemoteStore remote = new();
    private readonly InMemorySyncIndexStore index = new();
    private readonly Mock<ISyncFileStore> mockFiles = new();
    private readonly Mock<ISyncFileDecryptor> mockDecryptor = new();
    private readonly Mock<IS3SyncTargetProvider> mockTargets = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly FakeSyncProfileRegistry registry;

    public SyncFileHistoryServiceTests()
    {
        registry = FakeSyncProfileRegistry.Of(new SyncProfile(Profile, Mock.Of<ISyncFileStore>(), index, remote, new GlobSyncPathMatcher("notes/**/*", true)));
        registry.SetFileStores(Profile, mockFiles.Object, Mock.Of<ISyncFileStore>());
        registry.SetFileDecryptor(Profile, mockDecryptor.Object);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncFileHistoryService(null!, mockTargets.Object, mockUser.Object)).ParamName);
        Assert.Equal("targetProvider", Assert.Throws<ArgumentNullException>(() => new SyncFileHistoryService(registry, null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncFileHistoryService(registry, mockTargets.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenABucketOrNone_WhenAskedIfHistoryIsAvailable_ThenItSaysWhetherThereIsABucket(bool isConfigured)
    {
        // Arrange
        mockTargets.SetupGet(x => x.IsConfigured).Returns(isConfigured);

        // Act
        var available = CreateSut().IsAvailable;

        // Assert
        Assert.Equal(isConfigured, available);
    }

    [Fact]
    public async Task GivenAFileUploadedThreeTimes_WhenGetVersionsAsync_ThenEachIsListedNewestFirstWithTheOneThisDeviceSyncedMarked()
    {
        // Arrange
        remote.Seed(Key, [1], null);
        var synced = remote.Seed(Key, [1, 2], null);
        remote.Seed(Key, [1, 2, 3], null);
        await index.SaveAsync(new SyncIndexRecord { Profile = Profile, Owner = "bob", Key = Key, BaseETag = synced });

        // Act
        var versions = await CreateSut().GetVersionsAsync(Profile, Key);

        // Assert
        Assert.Equal(["v3", "v2", "v1"], versions.Select(v => v.VersionId));
        Assert.Equal([false, true, false], versions.Select(v => v.IsSynced));
        Assert.Equal([true, false, false], versions.Select(v => v.IsLatest));
        Assert.All(versions, v =>
        {
            Assert.Equal(Profile, v.Profile);
            Assert.Equal(Key, v.Key);
            Assert.Equal(remote.UploadTime, v.UploadedAt);
        });
        Assert.Equal("\"2\"", versions[1].ETag);
        Assert.Equal(2, versions[1].Size);
        Assert.DoesNotContain(remote.Calls, c => c.StartsWith("get ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenNoOneSignedInOrNothingSynced_WhenGetVersionsAsync_ThenNoVersionIsMarkedSynced()
    {
        // Arrange
        remote.Seed(Key, [1], null);
        var sut = CreateSut();

        // Act
        var unsynced = await sut.GetVersionsAsync(Profile, Key);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        var signedOut = await sut.GetVersionsAsync(Profile, Key);

        // Assert
        Assert.False(Assert.Single(unsynced).IsSynced);
        Assert.False(Assert.Single(signedOut).IsSynced);
    }

    [Fact]
    public async Task GivenAFileNeverUploaded_WhenGetVersionsAsync_ThenThereAreNone()
    {
        // Act
        var versions = await CreateSut().GetVersionsAsync(Profile, Key);

        // Assert
        Assert.Empty(versions);
    }

    [Fact]
    public async Task GivenAVersion_WhenReadVersionAsync_ThenItIsDownloadedAndDecryptedAtItsKey()
    {
        // Arrange
        remote.Seed(Key, [9, 9], null);
        remote.Seed(Key, [8], null);
        mockDecryptor.Setup(x => x.DecryptAsync(Key, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9 })))).ReturnsAsync([42]);
        var sut = CreateSut();
        var version = (await sut.GetVersionsAsync(Profile, Key))[1];

        // Act
        var read = await sut.ReadVersionAsync(version);

        // Assert
        Assert.Same(version, read!.Version);
        Assert.Equal([42], read.Content);
        Assert.Contains($"get {Key} v1", remote.Calls);
    }

    [Fact]
    public async Task GivenAVersionNoLongerInTheBucket_WhenReadVersionAsync_ThenNullAndNothingIsDecrypted()
    {
        // Arrange
        var gone = new SyncFileVersion { Profile = Profile, Key = Key, VersionId = "v9" };

        // Act
        var read = await CreateSut().ReadVersionAsync(gone);

        // Assert
        Assert.Null(read);
        mockDecryptor.Verify(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenAVersionRead_WhenRestoreAsync_ThenItsContentIsSavedThroughTheProfilesFileStore()
    {
        // Arrange
        var version = new SyncFileVersionContent { Version = new SyncFileVersion { Profile = Profile, Key = Key, VersionId = "v1" }, Content = [7] };

        // Act
        await CreateSut().RestoreAsync(version);

        // Assert
        mockFiles.Verify(x => x.WriteAsync(Key, version.Content, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(remote.Calls, c => c.StartsWith("put ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenNoProfileKeyOrVersion_WhenCalled_ThenThrowsArgument()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetVersionsAsync(" ", Key));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetVersionsAsync(Profile, string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadVersionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RestoreAsync(null!));
    }

    private SyncFileHistoryService CreateSut() => new(registry, mockTargets.Object, mockUser.Object);
}
