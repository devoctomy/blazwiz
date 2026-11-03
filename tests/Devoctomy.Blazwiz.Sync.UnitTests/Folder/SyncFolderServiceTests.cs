using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Folder;

public class SyncFolderServiceTests
{
    private readonly Mock<IFolderAccessClient> mockFolder = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISyncStorageResolver> mockResolver = new();
    private readonly SyncOptions options = new();
    private SyncStorageKind kind = SyncStorageKind.LocalFolder;

    public SyncFolderServiceTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        options.Profiles.Add(new SyncProfileOptions { Name = "photos" });
        options.Profiles.Add(new SyncProfileOptions { Name = "notes" });
        mockResolver.Setup(x => x.IsSwitchable("notes")).Returns(true);
        mockResolver.Setup(x => x.GetStateAsync("notes")).ReturnsAsync(() => new SyncStorageState { Kind = kind });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("folderClient", Assert.Throws<ArgumentNullException>(() => new SyncFolderService(null!, mockUser.Object, mockResolver.Object, options)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncFolderService(mockFolder.Object, null!, mockResolver.Object, options)).ParamName);
        Assert.Equal("resolver", Assert.Throws<ArgumentNullException>(() => new SyncFolderService(mockFolder.Object, mockUser.Object, null!, options)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncFolderService(mockFolder.Object, mockUser.Object, mockResolver.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenPickStatusAndReconnect_ThenKeptUnderTheirId()
    {
        // Arrange
        var status = new SyncFolderStatus { Name = "Notes" };
        mockFolder.Setup(x => x.IsSupportedAsync()).ReturnsAsync(true);
        mockFolder.Setup(x => x.PickAsync("bob")).ReturnsAsync("Notes");
        mockFolder.Setup(x => x.GetStatusAsync("bob")).ReturnsAsync(status);
        mockFolder.Setup(x => x.ReconnectAsync("bob")).ReturnsAsync(true);
        var sut = CreateSut();

        // Act & Assert
        Assert.True(await sut.IsSupportedAsync());
        Assert.Equal("Notes", await sut.PickAsync());
        Assert.Same(status, await sut.GetStatusAsync());
        Assert.True(await sut.ReconnectAsync());
    }

    [Theory]
    [InlineData(SyncStorageKind.LocalFolder, "prompt", true)]
    [InlineData(SyncStorageKind.LocalFolder, "granted", false)]
    [InlineData(SyncStorageKind.IndexedDb, "prompt", false)]
    public async Task GivenStorageAndPermission_WhenNeedsReconnectAsync_ThenOnlyForAFolderWithoutPermission(SyncStorageKind storage, string permission, bool expected)
    {
        // Arrange
        kind = storage;
        mockFolder.Setup(x => x.GetStatusAsync("bob")).ReturnsAsync(new SyncFolderStatus { Name = "Notes", Permission = permission });

        // Act & Assert
        Assert.Equal(expected, await CreateSut().NeedsReconnectAsync());
    }

    [Fact]
    public async Task GivenFolderInUseButNonePicked_WhenNeedsReconnectAsync_ThenTrue()
    {
        // Act & Assert
        Assert.True(await CreateSut().NeedsReconnectAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenPickAsync_ThenThrows()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().PickAsync());
    }

    private SyncFolderService CreateSut() => new(mockFolder.Object, mockUser.Object, mockResolver.Object, options);
}
