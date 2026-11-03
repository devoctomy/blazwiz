using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.Storage;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Storage;

public class PersistentStorageServiceTests
{
    private readonly Mock<IStorageDurabilityService> mockDurability = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private StorageDurabilityStatus status = new() { IsSupported = true };
    private StoragePersistenceState? state;

    public PersistentStorageServiceTests()
    {
        mockDurability.Setup(x => x.GetStatusAsync()).ReturnsAsync(() => status);
        mockLocalStorage.Setup(x => x.GetObjectAsync<StoragePersistenceState>(PersistentStorageService.StorageKey)).ReturnsAsync(() => state);
        mockLocalStorage.Setup(x => x.SetObjectAsync(PersistentStorageService.StorageKey, It.IsAny<StoragePersistenceState>()))
            .Callback<string, StoragePersistenceState>((_, s) => state = s)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("durabilityService", Assert.Throws<ArgumentNullException>(() => new PersistentStorageService(null!, mockLocalStorage.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new PersistentStorageService(mockDurability.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenFirstSignIn_WhenRequestAfterSignInAsync_ThenAskedOnceAndRemembered()
    {
        // Arrange
        mockDurability.Setup(x => x.RequestPersistenceAsync()).ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        await sut.RequestAfterSignInAsync();
        await sut.RequestAfterSignInAsync();

        // Assert
        mockDurability.Verify(x => x.RequestPersistenceAsync(), Times.Once);
        Assert.True(state!.Requested);
        Assert.False(state.Granted);
        Assert.False(state.InstalledWhenRefused);
    }

    [Fact]
    public async Task GivenRefusedThenInstalled_WhenRequestAfterSignInAsync_ThenAskedAgainOnce()
    {
        // Arrange
        state = new StoragePersistenceState { Requested = true };
        status = new StorageDurabilityStatus { IsSupported = true, IsInstalled = true };
        mockDurability.Setup(x => x.RequestPersistenceAsync()).ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        await sut.RequestAfterSignInAsync();
        await sut.RequestAfterSignInAsync();

        // Assert
        mockDurability.Verify(x => x.RequestPersistenceAsync(), Times.Once);
        Assert.True(state!.InstalledWhenRefused);
    }

    [Fact]
    public async Task GivenGranted_WhenRequestAfterSignInAsync_ThenNotAskedAgainEvenIfInstalled()
    {
        // Arrange
        state = new StoragePersistenceState { Requested = true, Granted = true };
        status = new StorageDurabilityStatus { IsSupported = true, IsInstalled = true };

        // Act
        await CreateSut().RequestAfterSignInAsync();

        // Assert
        mockDurability.Verify(x => x.RequestPersistenceAsync(), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GivenUnsupportedOrAlreadyPersisted_WhenRequestAfterSignInAsync_ThenNotAsked(bool supported, bool persisted)
    {
        // Arrange
        status = new StorageDurabilityStatus { IsSupported = supported, IsPersisted = persisted };

        // Act
        await CreateSut().RequestAfterSignInAsync();

        // Assert
        mockDurability.Verify(x => x.RequestPersistenceAsync(), Times.Never);
    }

    private PersistentStorageService CreateSut() => new(mockDurability.Object, mockLocalStorage.Object);
}
