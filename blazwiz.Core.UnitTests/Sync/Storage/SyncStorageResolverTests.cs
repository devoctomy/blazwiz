using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Sync.Storage;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Storage;

public class SyncStorageResolverTests
{
    private readonly Mock<IServiceProvider> mockServices = new();
    private readonly Mock<ISyncStorageStateStore> mockStateStore = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<IOpfsClient> mockOpfs = new();
    private readonly SyncOptions options = new();
    private int indexedDbCreated;

    public SyncStorageResolverTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        var notes = new SyncProfileOptions { Name = "notes", DefaultStorageKind = SyncStorageKind.Opfs };
        notes.FileStores.Add(new SyncStorageOptions(SyncStorageKind.IndexedDb, _ =>
        {
            indexedDbCreated++;
            return Mock.Of<ISyncFileStore>();
        }));
        notes.FileStores.Add(new SyncStorageOptions(SyncStorageKind.Opfs, _ => Mock.Of<ISyncFileStore>()));
        options.Profiles.Add(notes);
        options.Profiles.Add(new SyncProfileOptions { Name = "photos" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => new SyncStorageResolver(null!, options, mockStateStore.Object, mockUser.Object, mockOpfs.Object)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncStorageResolver(mockServices.Object, null!, mockStateStore.Object, mockUser.Object, mockOpfs.Object)).ParamName);
        Assert.Equal("stateStore", Assert.Throws<ArgumentNullException>(() => new SyncStorageResolver(mockServices.Object, options, null!, mockUser.Object, mockOpfs.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncStorageResolver(mockServices.Object, options, mockStateStore.Object, null!, mockOpfs.Object)).ParamName);
        Assert.Equal("opfsClient", Assert.Throws<ArgumentNullException>(() => new SyncStorageResolver(mockServices.Object, options, mockStateStore.Object, mockUser.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenProfiles_WhenIsSwitchableAndGetKinds_ThenOnlyConfiguredOnes()
    {
        var sut = CreateSut();
        Assert.True(sut.IsSwitchable("notes"));
        Assert.False(sut.IsSwitchable("photos"));
        Assert.False(sut.IsSwitchable("missing"));
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs], sut.GetKinds("notes"));
        Assert.Empty(sut.GetKinds("photos"));
        Assert.Empty(sut.GetKinds("missing"));
        Assert.Throws<ArgumentException>(() => sut.IsSwitchable(" "));
    }

    [Fact]
    public void GivenStorage_WhenGetStoreTwice_ThenCreatedOnceFromFactory()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = sut.GetStore("notes", SyncStorageKind.IndexedDb);
        var second = sut.GetStore("notes", SyncStorageKind.IndexedDb);

        // Assert
        Assert.Same(first, second);
        Assert.Equal(1, indexedDbCreated);
        Assert.NotSame(first, sut.GetStore("notes", SyncStorageKind.Opfs));
    }

    [Fact]
    public void GivenStorageNotConfigured_WhenGetStore_ThenThrows()
    {
        var sut = CreateSut();
        Assert.Contains("has no Opfs storage", Assert.Throws<ArgumentException>(() => sut.GetStore("photos", SyncStorageKind.Opfs)).Message);
    }

    [Fact]
    public async Task GivenNoRecordedState_WhenGetStateAsync_ThenDefaultStorage()
    {
        // Act
        var state = await CreateSut().GetStateAsync("notes");

        // Assert
        Assert.Equal(("notes", "bob", SyncStorageKind.Opfs), (state.Profile, state.Owner, state.Kind));
        Assert.Null(state.MigrationPhase);
    }

    [Fact]
    public async Task GivenRecordedState_WhenGetStateAsync_ThenReturnsIt()
    {
        // Arrange
        var recorded = new SyncStorageState { Profile = "notes", Owner = "bob", Kind = SyncStorageKind.IndexedDb, MigrationPhase = SyncStorageMigrationPhase.Copying };
        mockStateStore.Setup(x => x.GetAsync("notes", "bob")).ReturnsAsync(recorded);

        // Act & Assert
        Assert.Same(recorded, await CreateSut().GetStateAsync("notes"));
    }

    [Fact]
    public async Task GivenRecordedStorageNoLongerConfigured_WhenGetStateAsync_ThenDefaultStorage()
    {
        // Arrange
        options.Profiles[0].FileStores.RemoveAt(0);
        mockStateStore.Setup(x => x.GetAsync("notes", "bob")).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });

        // Act & Assert
        Assert.Equal(SyncStorageKind.Opfs, (await CreateSut().GetStateAsync("notes")).Kind);
    }

    [Fact]
    public async Task GivenProfileCannotSwitchOrSignedOut_WhenGetStateAsync_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetStateAsync("photos"));
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetStateAsync("notes"));
    }

    [Fact]
    public async Task GivenStorages_WhenIsSupportedAsync_ThenIndexedDbAlwaysAndOpfsAsTheBrowserSays()
    {
        // Arrange
        mockOpfs.Setup(x => x.IsSupportedAsync()).ReturnsAsync(false);
        var sut = CreateSut();

        // Act & Assert
        Assert.True(await sut.IsSupportedAsync(SyncStorageKind.IndexedDb));
        Assert.False(await sut.IsSupportedAsync(SyncStorageKind.Opfs));
        Assert.False(await sut.IsSupportedAsync((SyncStorageKind)99));
    }

    private SyncStorageResolver CreateSut() => new(mockServices.Object, options, mockStateStore.Object, mockUser.Object, mockOpfs.Object);
}
