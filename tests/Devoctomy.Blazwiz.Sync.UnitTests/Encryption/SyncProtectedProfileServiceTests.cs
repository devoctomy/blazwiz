using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
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
    private readonly Mock<IE2eLockChannel> mockChannel = new();
    private readonly Mock<IE2eIdleLock> mockIdle = new();
    private readonly Mock<IE2eProfileLabelStore> mockLabels = new();
    private readonly Dictionary<string, string> labels = [];
    private readonly List<string> calls = [];
    private readonly E2eKeyScope notes = new(Guid.NewGuid().ToString("D"));

    public SyncProtectedProfileServiceTests()
    {
        options.Profiles.Add(new SyncProfileOptions { Name = "notes", RemotePattern = "notes/**/*.md", KeyScope = notes });
        registry = FakeSyncProfileRegistry.Of(options);
        mockLabels.Setup(x => x.GetAllAsync()).ReturnsAsync(() => new Dictionary<string, string>(labels));
        mockLabels.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((id, label) => labels[id] = label).Returns(Task.CompletedTask);
        mockLabels.Setup(x => x.RemoveAsync(It.IsAny<string>())).Callback<string>(id => labels.Remove(id)).Returns(Task.CompletedTask);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => calls.Add("lock " + name))
            .ReturnsAsync(new Releaser(calls));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(null!, registry, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, null!, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("storageResolver", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, null!, mockUser.Object, mockLock.Object, mockChannel.Object, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, null!, mockLock.Object, mockChannel.Object, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, null!, mockChannel.Object, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("lockChannel", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object, null!, options, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, null!, mockIdle.Object, mockLabels.Object)).ParamName);
        Assert.Equal("idleLock", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, options, null!, mockLabels.Object)).ParamName);
        Assert.Equal("labelStore", Assert.Throws<ArgumentNullException>(() => new SyncProtectedProfileService(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, options, mockIdle.Object, null!)).ParamName);
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
        Assert.Equal($"profiles/{first}/**/*", profile.RemotePattern);
        Assert.True(profile.IncludeEmptyFolders);
        Assert.Equal($"{first}_index", profile.ResolvedIndexStoreName);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], profile.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.IndexedDb, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public async Task GivenAProfileDeletedElsewhere_WhenLoadedAgain_ThenThisDevicesKeysFilesAndIndexAreDeletedAndItIsRemoved()
    {
        // Arrange
        var kept = Guid.NewGuid().ToString("D");
        var deleted = Guid.NewGuid().ToString("D");
        mockManager.SetupSequence(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new E2eProfileIndexEntry { Id = kept }, new E2eProfileIndexEntry { Id = deleted }])
            .ReturnsAsync([new E2eProfileIndexEntry { Id = kept }]);
        mockManager.Setup(x => x.ForgetAsync(new E2eKeyScope(deleted))).Callback(() => calls.Add("forget keys")).Returns(Task.CompletedTask);
        var index = new Mock<ISyncIndexStore>();
        index.Setup(x => x.GetAllAsync(deleted, "bob", It.IsAny<CancellationToken>())).ReturnsAsync([new SyncIndexRecord { Key = $"profiles/{deleted}/a" }]);
        index.Setup(x => x.DeleteAsync(deleted, "bob", It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("delete index")).Returns(Task.CompletedTask);
        registry.SetSyncProfile(new SyncProfile(deleted, Mock.Of<ISyncFileStore>(), index.Object, Mock.Of<ISyncRemoteStore>(), Mock.Of<ISyncPathMatcher>()));
        mockResolver.Setup(x => x.GetStateAsync(deleted)).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });
        mockResolver.Setup(x => x.GetKinds(deleted)).Returns([SyncStorageKind.IndexedDb]);
        mockResolver.Setup(x => x.GetStore(deleted, SyncStorageKind.IndexedDb)).Returns(StoreWith($"profiles/{deleted}/a", "indexeddb").Object);
        var sut = CreateSut();
        await sut.LoadAsync();

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.Equal(["notes", kept], registry.Profiles.Select(p => p.Name));
        Assert.Equal(["forget keys", "lock sync-" + deleted, "delete index", "delete indexeddb", "release"], calls);
    }

    [Fact]
    public async Task GivenAPassphrase_WhenCreated_ThenTheNewProfileIsAdded()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.CreateAsync("pass", options.ProfileDefaults, null, It.IsAny<CancellationToken>())).ReturnsAsync(scope);
        var sut = CreateSut();
        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        var result = await sut.CreateAsync("pass");

        // Assert
        Assert.Equal(scope, result);
        Assert.Equal(scope, registry.Find(scope.Id)!.KeyScope);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenAProtectedProfile_WhenDeleted_ThenUnderItsLockItsKeysGoThenItsFilesThenTheProfile()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.CreateAsync("pass", options.ProfileDefaults, null, It.IsAny<CancellationToken>())).ReturnsAsync(scope);
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

        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        await sut.DeleteAsync(scope);

        // Assert
        Assert.Equal(
            ["lock sync-" + scope.Id, "delete keys", $"delete remote profiles/{scope.Id}/a", "delete index", "delete indexeddb", "delete opfs", "release"],
            calls);
        mockResolver.Verify(x => x.GetStore(scope.Id, SyncStorageKind.LocalFolder), Times.Never);
        Assert.Null(registry.Find(scope.Id));
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenS3CannotBeReached_WhenDeleted_ThenThrowsAndNothingIsDeleted()
    {
        // Arrange
        var (sut, scope, remote) = await CreateDeletableProfileAsync();
        remote.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable));

        // Act & Assert
        await Assert.ThrowsAsync<SyncNetworkException>(() => sut.DeleteAsync(scope));
        mockManager.Verify(x => x.DeleteAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(registry.Find(scope.Id));
    }

    [Fact]
    public async Task GivenNoSyncTarget_WhenDeleted_ThenItsKeysAndLocalFilesAreDeleted()
    {
        // Arrange
        var (sut, scope, remote) = await CreateDeletableProfileAsync();
        remote.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new SyncAccessDeniedException(SyncErrorCodes.RemoteNoCredentials));

        // Act
        await sut.DeleteAsync(scope);

        // Assert
        mockManager.Verify(x => x.DeleteAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
        remote.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(registry.Find(scope.Id));
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenDeleted_ThenThrowsAndNothingIsDeleted()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().DeleteAsync(new E2eKeyScope("other")));
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

    [Fact]
    public async Task GivenLoaded_WhenAProfileIsLockedInThisTab_ThenTheOtherTabsAreTold()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        mockManager.Setup(x => x.IsUnlocked(scope)).Returns(false);
        await CreateSut().LoadAsync();

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockChannel.Verify(x => x.StartAsync(), Times.Once);
        mockChannel.Verify(x => x.PostLockAsync(scope.Id), Times.Once);
    }

    [Fact]
    public async Task GivenLoaded_WhenAProfileIsUnlockedInThisTab_ThenNothingIsSent()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        mockManager.Setup(x => x.IsUnlocked(scope)).Returns(true);
        await CreateSut().LoadAsync();

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockChannel.Verify(x => x.PostLockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenLoaded_WhenAnotherTabLocksAProfile_ThenItIsLockedHere()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        await CreateSut().LoadAsync();

        // Act
        mockChannel.Raise(x => x.LockReceived += null, mockChannel.Object, scope.Id);
        mockChannel.Raise(x => x.LockReceived += null, mockChannel.Object, "notes");

        // Assert
        mockManager.Verify(x => x.Lock(scope), Times.Once);
        mockManager.Verify(x => x.Lock(It.Is<E2eKeyScope>(s => s.Id == "notes")), Times.Never);
    }

    [Fact]
    public async Task GivenLoadedTwice_WhenAProfileIsLocked_ThenTheOtherTabsAreToldOnce()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        var sut = CreateSut();
        await sut.LoadAsync();
        await sut.LoadAsync();

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockChannel.Verify(x => x.StartAsync(), Times.Once);
        mockChannel.Verify(x => x.PostLockAsync(scope.Id), Times.Once);
    }

    [Fact]
    public async Task GivenSettingsNotAllowingDeviceUnlock_WhenUsed_ThenThrowsAndTheManagerIsNotCalled()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        var sut = CreateSut();
        await sut.LoadAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnableDeviceUnlockAsync(scope));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UnlockWithDeviceAsync(scope));
        mockManager.Verify(x => x.EnableDeviceUnlockAsync(It.IsAny<E2eKeyScope>()), Times.Never);
        mockManager.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenSettingsAllowingDeviceUnlock_WhenUsed_ThenTheManagerIsCalled()
    {
        // Arrange
        options.ProfileDefaults.AllowDeviceUnlock = true;
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }]);
        var sut = CreateSut();
        await sut.LoadAsync();

        // Act
        await sut.EnableDeviceUnlockAsync(scope);
        await sut.UnlockWithDeviceAsync(scope);

        // Assert
        mockManager.Verify(x => x.EnableDeviceUnlockAsync(scope), Times.Once);
        mockManager.Verify(x => x.UnlockWithDeviceAsync(scope), Times.Once);
    }

    [Fact]
    public async Task GivenAProfileSetUpInCode_WhenLoadedAndUnloaded_ThenItStays()
    {
        // Arrange
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = notes.Id }]);
        var sut = CreateSut();

        // Act
        await sut.LoadAsync();
        sut.Unload();

        // Assert
        Assert.Equal(["notes"], registry.Profiles.Select(p => p.Name));
        mockManager.Verify(x => x.LockAll(), Times.Once);
    }

    [Fact]
    public async Task GivenAProfileSetUpInCode_WhenSetUpAndItsPassphraseChanged_ThenItsSettingsAreUsed()
    {
        // Arrange
        var sut = CreateSut();
        var settings = options.Profiles[0].Settings;

        // Act
        await sut.SetUpAsync(notes, "correct horse");
        await sut.ChangePassphraseAsync(notes, "correct horse", "battery staple");

        // Assert
        mockManager.Verify(x => x.CreateKeysAsync(notes, "correct horse", settings, null, It.IsAny<CancellationToken>()), Times.Once);
        mockManager.Verify(x => x.ChangePassphraseAsync(notes, "correct horse", "battery staple", settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenTheIndex_WhenLoaded_ThenProfilesListThoseInCodeThenThoseCreatedWithWhetherEachIsSetUp()
    {
        // Arrange
        var created = Guid.NewGuid().ToString("D");
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = created }]);
        var sut = CreateSut();
        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        var before = sut.Profiles;
        await sut.LoadAsync();

        // Assert
        Assert.Empty(before);
        Assert.True(sut.IsLoaded);
        Assert.Equal(1, raised);
        Assert.Collection(
            sut.Profiles,
            p =>
            {
                Assert.Equal("notes", p.Name);
                Assert.Equal(notes, p.Scope);
                Assert.False(p.IsCreatedByUser);
                Assert.False(p.IsSetUp);
            },
            p =>
            {
                Assert.Equal(created, p.Name);
                Assert.True(p.IsCreatedByUser);
                Assert.True(p.IsSetUp);
            });
    }

    [Fact]
    public async Task GivenLabelsOnThisDevice_WhenLoaded_ThenTheProfilesTheUserCreatedHaveThem()
    {
        // Arrange
        var created = Guid.NewGuid().ToString("D");
        labels[created] = "Work";
        labels[notes.Id] = "ignored";
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = created }]);
        var sut = CreateSut();

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.Null(sut.Profiles.Single(p => p.Name == "notes").Label);
        Assert.Equal("Work", sut.Profiles.Single(p => p.Name == created).Label);
    }

    [Fact]
    public async Task GivenAProfileTheUserCreated_WhenLabelled_ThenTheLabelIsKeptAndProfilesChangedIsRaisedOnce()
    {
        // Arrange
        var created = Guid.NewGuid().ToString("D");
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = created }]);
        var sut = CreateSut();
        await sut.LoadAsync();
        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        await sut.SetLabelAsync(new E2eKeyScope(created), "Work");
        await sut.SetLabelAsync(new E2eKeyScope(created), "Work");

        // Assert
        Assert.Equal("Work", labels[created]);
        Assert.Equal("Work", sut.Profiles.Single(p => p.Name == created).Label);
        Assert.Equal(1, raised);
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetLabelAsync(notes, "Notes"));
    }

    [Fact]
    public async Task GivenALabelledProfile_WhenDeletedHereOrElsewhere_ThenItsLabelIsRemoved()
    {
        // Arrange
        var (sut, scope, remote) = await CreateDeletableProfileAsync();
        remote.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        labels[scope.Id] = "Work";
        var elsewhere = Guid.NewGuid().ToString("D");
        labels[elsewhere] = "Home";
        mockManager.SetupSequence(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new E2eProfileIndexEntry { Id = scope.Id }, new E2eProfileIndexEntry { Id = elsewhere }])
            .ReturnsAsync([]);
        await sut.LoadAsync();
        CreateEmptyLocalProfile(elsewhere);

        // Act
        await sut.DeleteAsync(scope);
        await sut.LoadAsync();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GivenAProfileSetUpInCode_WhenItsPassphraseIsSet_ThenItIsSetUpAndProfilesChangedIsRaised()
    {
        // Arrange
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();
        await sut.LoadAsync();
        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        await sut.SetUpAsync(notes, "correct horse battery");

        // Assert
        Assert.True(Assert.Single(sut.Profiles).IsSetUp);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenLoaded_WhenUnloaded_ThenNoProfilesAndProfilesChangedIsRaised()
    {
        // Arrange
        mockManager.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new E2eProfileIndexEntry { Id = notes.Id }]);
        var sut = CreateSut();
        await sut.LoadAsync();
        var raised = 0;
        sut.ProfilesChanged += (_, _) => raised++;

        // Act
        sut.Unload();

        // Assert
        Assert.False(sut.IsLoaded);
        Assert.Empty(sut.Profiles);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenAProfileSetUpInCode_WhenDeleted_ThenThrowsAndNothingIsDeleted()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().DeleteAsync(notes));
        mockManager.Verify(x => x.DeleteAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Mock<ISyncFileStore> StoreWith(string key, string label)
    {
        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new SyncFileInfo { Key = key }]);
        store.Setup(x => x.DeleteAsync(key, It.IsAny<CancellationToken>())).Callback(() => calls.Add("delete " + label)).Returns(Task.CompletedTask);
        return store;
    }

    private async Task<(SyncProtectedProfileService Sut, E2eKeyScope Scope, Mock<ISyncRemoteStore> Remote)> CreateDeletableProfileAsync()
    {
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockManager.Setup(x => x.CreateAsync("pass", options.ProfileDefaults, null, It.IsAny<CancellationToken>())).ReturnsAsync(scope);
        var sut = CreateSut();
        await sut.CreateAsync("pass");
        var remote = new Mock<ISyncRemoteStore>();
        var index = new Mock<ISyncIndexStore>();
        index.Setup(x => x.GetAllAsync(scope.Id, "bob", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        registry.SetSyncProfile(new SyncProfile(scope.Id, Mock.Of<ISyncFileStore>(), index.Object, remote.Object, Mock.Of<ISyncPathMatcher>()));
        mockResolver.Setup(x => x.GetStateAsync(scope.Id)).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });
        mockResolver.Setup(x => x.GetKinds(scope.Id)).Returns([SyncStorageKind.IndexedDb]);
        mockResolver.Setup(x => x.GetStore(scope.Id, SyncStorageKind.IndexedDb)).Returns(StoreWith("profiles/a", "indexeddb").Object);
        return (sut, scope, remote);
    }

    private void CreateEmptyLocalProfile(string id)
    {
        var index = new Mock<ISyncIndexStore>();
        index.Setup(x => x.GetAllAsync(id, "bob", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        registry.SetSyncProfile(new SyncProfile(id, Mock.Of<ISyncFileStore>(), index.Object, Mock.Of<ISyncRemoteStore>(), Mock.Of<ISyncPathMatcher>()));
        mockResolver.Setup(x => x.GetStateAsync(id)).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });
        mockResolver.Setup(x => x.GetKinds(id)).Returns([SyncStorageKind.IndexedDb]);
        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mockResolver.Setup(x => x.GetStore(id, SyncStorageKind.IndexedDb)).Returns(store.Object);
    }

    private SyncProtectedProfileService CreateSut() => new(mockManager.Object, registry, mockResolver.Object, mockUser.Object, mockLock.Object, mockChannel.Object, options, mockIdle.Object, mockLabels.Object);

    private sealed class Releaser(List<string> calls) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            calls.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}
