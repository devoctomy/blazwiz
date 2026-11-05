using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class LocalStorageE2eLocalProfileIndexStoreTests
{
    private const string StorageKey = LocalStorageE2eLocalProfileIndexStore.StorageKeyPrefix + "bob@example.com";

    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<ISyncUserContext> mockUser = new();

    public LocalStorageE2eLocalProfileIndexStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob@example.com");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eLocalProfileIndexStore(null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eLocalProfileIndexStore(mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenASavedIndex_WhenGetAsync_ThenReadsTheUsersEntry()
    {
        // Arrange
        mockLocalStorage.Setup(x => x.GetItemAsync(StorageKey)).ReturnsAsync("{}");

        // Act & Assert
        Assert.Equal("{}", await CreateSut().GetAsync());
    }

    [Fact]
    public async Task GivenAnIndex_WhenSaveAsync_ThenWrittenUnderTheUsersEntry()
    {
        // Act
        await CreateSut().SaveAsync("{}");

        // Assert
        mockLocalStorage.Verify(x => x.SetItemAsync(StorageKey, "{}"), Times.Once);
    }

    [Fact]
    public async Task GivenNullValue_WhenSaveAsync_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().SaveAsync(null!));
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAsync());
    }

    private LocalStorageE2eLocalProfileIndexStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object);
}
