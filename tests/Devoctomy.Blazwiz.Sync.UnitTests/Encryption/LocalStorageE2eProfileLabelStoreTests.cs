using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class LocalStorageE2eProfileLabelStoreTests
{
    private readonly Dictionary<string, string> storage = [];
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<ISyncUserContext> mockUser = new();

    public LocalStorageE2eProfileLabelStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLocalStorage.Setup(x => x.GetItemAsync(It.IsAny<string>())).ReturnsAsync((string key) => storage.GetValueOrDefault(key));
        mockLocalStorage.Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((k, v) => storage[k] = v).Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenLabels_WhenSetAndRemoved_ThenKeptUnderTheUser()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetAsync("p1", "Work");
        await sut.SetAsync("p2", "Home");
        await sut.RemoveAsync("p2");

        // Assert
        Assert.Equal(new Dictionary<string, string> { ["p1"] = "Work" }, await sut.GetAllAsync());
        Assert.Contains("Work", storage[LocalStorageE2eProfileLabelStore.StorageKeyPrefix + "bob"]);
    }

    [Fact]
    public async Task GivenUnreadableLabels_WhenGot_ThenNone()
    {
        // Arrange
        storage[LocalStorageE2eProfileLabelStore.StorageKeyPrefix + "bob"] = "not json";

        // Act & Assert
        Assert.Empty(await CreateSut().GetAllAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenUsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAllAsync());
    }

    private LocalStorageE2eProfileLabelStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object);
}
