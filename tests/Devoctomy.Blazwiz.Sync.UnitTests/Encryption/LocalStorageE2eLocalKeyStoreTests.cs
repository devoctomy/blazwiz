using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class LocalStorageE2eLocalKeyStoreTests
{
    private const string StorageKey = LocalStorageE2eLocalKeyStore.StorageKeyPrefix + "bob@example.com";

    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<ISyncUserContext> mockUser = new();

    public LocalStorageE2eLocalKeyStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob@example.com");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eLocalKeyStore(null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eLocalKeyStore(mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSavedKeys_WhenGetAsync_ThenReadsTheUsersEntry()
    {
        // Arrange
        var keys = new E2eLocalKeys { WrappedMasterKey = new WrappedKey { Nonce = "n" }, CachedDocument = "{}" };
        mockLocalStorage.Setup(x => x.GetObjectAsync<E2eLocalKeys>(StorageKey)).ReturnsAsync(keys);

        // Act & Assert
        Assert.Same(keys, await CreateSut().GetAsync());
    }

    [Fact]
    public async Task GivenKeys_WhenSaveAsync_ThenWrittenUnderTheUsersEntry()
    {
        // Arrange
        var keys = new E2eLocalKeys();

        // Act
        await CreateSut().SaveAsync(keys);

        // Assert
        mockLocalStorage.Verify(x => x.SetObjectAsync(StorageKey, keys), Times.Once);
    }

    [Fact]
    public async Task GivenNullKeys_WhenSaveAsync_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().SaveAsync(null!));
    }

    [Fact]
    public async Task GivenKeys_WhenDeleteAsync_ThenUsersEntryRemoved()
    {
        // Act
        await CreateSut().DeleteAsync();

        // Assert
        mockLocalStorage.Verify(x => x.RemoveItemAsync(StorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAsync());
    }

    private LocalStorageE2eLocalKeyStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object);
}
