using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class SyncProtectedProfileServiceTests
{
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly SyncOptions options = new();
    private readonly FakeSyncProfileRegistry registry;
    private readonly Mock<ISyncStorageResolver> mockResolver = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISyncLockService> mockLock = new();
    private readonly List<string> calls = [];

    public SyncProtectedProfileServiceTests()
    {
        options.Profiles.Add(new SyncProfileOptions { Name = "notes", RemotePattern = "notes/**/*.md" });
        registry = FakeSyncProfileRegistry.Of(options);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add("lock " + name))
            .ReturnsAsync(new Releaser(calls));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(null!, registry, mockResolver.Object, mockUser.Object, mockLock.Object)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, null!, mockResolver.Object, mockUser.Object, mockLock.Object)).ParamName);
        Assert.Equal("storageResolver", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, null!, mockUser.Object, mockLock.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, null!, mockLock.Object)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheUsersProtectedProfiles_WhenLoaded_ThenEachIsAProfileOfItsOwn()
    {
        // Arrange
        var first = Guid.NewGuid().ToString("D");
        var second = Guid.NewGuid().ToString("D");
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = first }, new E2eProfileIndexEntry { Id = second }]);

        // Act
        var scopes = await CreateSut().LoadAsync();

        // Assert
        Assert.Equal([first, second], scopes.Select(s => s.Id));
        Assert.Equal(["notes", first, second], registry.Profiles.Select(p => p.Name));
        var profile = registry.Find(first)!;
        Assert.Equal(new E2eKeyScope(first), profile.KeyScope);
        Assert.True(profile.IsProtected);
        Assert.False(profile.IsEncrypted);
        Assert.Equal($"profiles/{first}/**/*", profile.RemotePattern);
        Assert.True(profile.IncludeEmptyFolders);
        Assert.Equal($"{first}_index", profile.ResolvedIndexStoreName);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], profile.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.IndexedDb, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public async Task GivenAProfileDeletedElsewhere_WhenLoadedAgain_ThenItIsLockedAndRemoved()
    {
        // Arrange
        var kept = Guid.NewGuid().ToString("D");
        var deleted = Guid.NewGuid().ToString("D");
        mockManager.SetupSequence(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new E2eProfileIndexEntry { Id = kept }, new E2eProfileIndexEntry { Id = deleted }])
            .ReturnsAsync([new E2eProfileIndexEntry { Id = kept }]);
        var sut = CreateSut();
        await sut.LoadAsync();

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.Equal(["notes", kept], registry.Profiles.Select(p => p.Name));
        mockManager.Verify(x => x.Lock(new E2eKeyScope(deleted)), Times.Once);
    }

    [Fact]
    public async Task GivenAPassphrase_WhenCreated_ThenTheNewProfileIsAdded()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.CreateAsync("pass", null, It.IsAny<CancellationToken>())).ReturnsAsync(scope);

        // Act
        var result = await CreateSut().CreateAsync("pass");

        // Assert
        Assert.Equal(scope, result);
        Assert.Equal(scope, registry.Find(scope.Id)!.KeyScope);
    }

    [Fact]
    public async Task GivenAProtectedProfile_WhenDeleted_ThenKeysGoFirstThenItsFilesUnderItsLockThenTheProfile()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.CreateAsync("pass", null, It.IsAny<CancellationToken>())).ReturnsAsync(scope);
        mockManager.Setup(x => x.DeleteAsync(scope, It.IsAny<CancellationToken>())).Callback(() => calls.Add("delete keys")).Returns(Task.CompletedTask);
        var sut = CreateSut();
        await sut.CreateAsync("pass");

        var remote = new Mock<ISyncRemoteStore>();
        remote.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new RemoteObjectInfo { Key = $"profiles/{scope.Id}/a" }]);
        remote.Setup(x => x.DeleteAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).Callback<string, string?, CancellationToken>((key, _, _) => calls.Add("delete remote " + key)).Returns(Task.CompletedTask);
        var index = new Mock<ISyncIndexStore>();
        index.Setup(x => x.GetAllAsync(scope.Id, "bob", It.IsAny<CancellationToken>())).ReturnsAsync([new SyncIndexRecord { Key = $"profiles/{scope.Id}/a" }]);
        index.Setup(x => x.DeleteAsync(scope.Id, "bob", It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("delete index")).Returns(Task.CompletedTask);
        registry.SetSyncProfile(new SyncProfile(scope.Id, Mock.Of<ISyncFileStore>(), index.Object, remote.Object, Mock.Of<ISyncPathMatcher>()));

        var indexedDb = StoreWith("profiles/a", "indexeddb");
        var opfs = StoreWith("profiles/b", "opfs");
        mockResolver.Setup(x => x.GetStateAsync(scope.Id)).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });
        mockResolver.Setup(x => x.GetKinds(scope.Id)).Returns([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder]);
        mockResolver.Setup(x => x.GetStore(scope.Id, SyncStorageKind.IndexedDb)).Returns(indexedDb.Object);
        mockResolver.Setup(x => x.GetStore(scope.Id, SyncStorageKind.Opfs)).Returns(opfs.Object);

        // Act
        await sut.DeleteAsync(scope);

        // Assert
        Assert.Equal(
            ["delete keys", "lock sync-" + scope.Id, $"delete remote profiles/{scope.Id}/a", "delete index", "delete indexeddb", "delete opfs", "release"],
            calls);
        mockResolver.Verify(x => x.GetStore(scope.Id, SyncStorageKind.LocalFolder), Times.Never);
        Assert.Null(registry.Find(scope.Id));
    }

    [Fact]
    public async Task GivenAConfiguredProfile_WhenDeleted_ThenThrowsAndNothingIsDeleted()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().DeleteAsync(new E2eKeyScope("notes")));
        mockManager.Verify(x => x.DeleteAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenProtectedProfiles_WhenUnloaded_ThenAllAreLockedAndRemoved()
    {
        // Arrange
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = Guid.NewGuid().ToString("D") }]);
        var sut = CreateSut();
        await sut.LoadAsync();

        // Act
        sut.Unload();

        // Assert
        mockManager.Verify(x => x.LockAll(), Times.Once);
        Assert.Equal(["notes"], registry.Profiles.Select(p => p.Name));
    }

    private Mock<ISyncFileStore> StoreWith(string key, string label)
    {
        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new SyncFileInfo { Key = key }]);
        store.Setup(x => x.DeleteAsync(key, It.IsAny<CancellationToken>())).Callback(() => calls.Add("delete " + label)).Returns(Task.CompletedTask);
        return store;
    }

    private SyncProtectedProfileService CreateSut() => new(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object);

    private sealed class Releaser(List<string> calls) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            calls.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}
