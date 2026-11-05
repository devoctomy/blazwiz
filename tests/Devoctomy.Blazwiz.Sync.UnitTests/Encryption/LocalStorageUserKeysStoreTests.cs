using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class LocalStorageUserKeysStoreTests
{
    private readonly Dictionary<string, string> storage = [];
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly E2eKeyScope scope = new("s1");

    public LocalStorageUserKeysStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLocalStorage.Setup(x => x.GetItemAsync(It.IsAny<string>())).ReturnsAsync((string key) => storage.GetValueOrDefault(key));
        mockLocalStorage.Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((k, v) => storage[k] = v).Returns(Task.CompletedTask);
        mockLocalStorage.Setup(x => x.RemoveItemAsync(It.IsAny<string>())).Callback<string>(k => storage.Remove(k)).Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageUserKeysStore(null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new LocalStorageUserKeysStore(mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAKeysDocument_WhenSetAndGot_ThenKeptUnderTheScopeAndUser()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetAsync(scope, "keys");

        // Assert
        Assert.Equal("keys", await sut.GetAsync(scope));
        Assert.Equal("keys", storage[LocalStorageUserKeysStore.KeysPrefix + "s1_bob"]);
        Assert.Null(await sut.GetAsync(new E2eKeyScope("s2")));
    }

    [Fact]
    public async Task GivenExistingKeys_WhenTryCreate_ThenFalseAndKept()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.TryCreateAsync(scope, "first");
        var second = await sut.TryCreateAsync(scope, "second");

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal("first", await sut.GetAsync(scope));
    }

    [Fact]
    public async Task GivenKeys_WhenDeleted_ThenGone()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetAsync(scope, "keys");

        // Act
        await sut.DeleteAsync(scope);

        // Assert
        Assert.Null(await sut.GetAsync(scope));
    }

    [Fact]
    public async Task GivenAnIndex_WhenSetAndGot_ThenKeptUnderTheUser()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetIndexAsync("index");

        // Assert
        Assert.Equal("index", await sut.GetIndexAsync());
        Assert.Equal("index", storage[LocalStorageUserKeysStore.IndexPrefix + "bob"]);
    }

    [Fact]
    public async Task GivenSignedOut_WhenUsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAsync(scope));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetIndexAsync());
    }

    [Fact]
    public async Task GivenNullArguments_WhenUsed_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryCreateAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetIndexAsync(null!));
    }

    private LocalStorageUserKeysStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object);
}
