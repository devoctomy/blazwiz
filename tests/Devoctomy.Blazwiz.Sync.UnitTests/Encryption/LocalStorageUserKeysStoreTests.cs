using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
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
    public async Task GivenAKeysDocument_WhenCreatedAndGot_ThenKeptUnderTheScopeAndUserWithItsVersion()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var etag = await sut.TryCreateAsync(scope, "keys");

        // Assert
        var stored = await sut.GetAsync(scope);
        Assert.NotNull(stored);
        Assert.Equal("keys", stored.Value);
        Assert.Equal(etag, stored.ETag);
        Assert.Contains("keys", storage[LocalStorageUserKeysStore.KeysPrefix + "s1_bob"]);
        Assert.Null(await sut.GetAsync(new E2eKeyScope("s2")));
    }

    [Fact]
    public async Task GivenExistingKeys_WhenTryCreate_ThenNullAndKept()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.TryCreateAsync(scope, "first");
        var second = await sut.TryCreateAsync(scope, "second");

        // Assert
        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal("first", (await sut.GetAsync(scope))!.Value);
    }

    [Fact]
    public async Task GivenKeysAtTheVersionRead_WhenSet_ThenReplacedWithANewVersion()
    {
        // Arrange
        var sut = CreateSut();
        var etag = await sut.TryCreateAsync(scope, "first");

        // Act
        var next = await sut.SetAsync(scope, "second", etag!);

        // Assert
        Assert.NotEqual(etag, next);
        var stored = await sut.GetAsync(scope);
        Assert.Equal("second", stored!.Value);
        Assert.Equal(next, stored.ETag);
    }

    [Fact]
    public async Task GivenKeysChangedSinceRead_WhenSet_ThenThrowsE2eDocumentChangedExceptionAndKept()
    {
        // Arrange
        var sut = CreateSut();
        var etag = await sut.TryCreateAsync(scope, "first");
        await sut.SetAsync(scope, "other device", etag!);

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetAsync(scope, "second", etag!));
        Assert.Equal("other device", (await sut.GetAsync(scope))!.Value);
    }

    [Fact]
    public async Task GivenNoKeys_WhenSet_ThenThrowsE2eDocumentChangedException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetAsync(scope, "keys", "1"));
        Assert.Null(await sut.GetAsync(scope));
    }

    [Fact]
    public async Task GivenKeys_WhenDeleted_ThenGone()
    {
        // Arrange
        var sut = CreateSut();
        await sut.TryCreateAsync(scope, "keys");

        // Act
        await sut.DeleteAsync(scope);

        // Assert
        Assert.Null(await sut.GetAsync(scope));
    }

    [Fact]
    public async Task GivenNoIndex_WhenSetWithoutAVersionAndGot_ThenKeptUnderTheUser()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var etag = await sut.SetIndexAsync("index", null);

        // Assert
        var stored = await sut.GetIndexAsync();
        Assert.Equal("index", stored!.Value);
        Assert.Equal(etag, stored.ETag);
        Assert.Contains("index", storage[LocalStorageUserKeysStore.IndexPrefix + "bob"]);
    }

    [Fact]
    public async Task GivenAnIndex_WhenSetWithoutAVersion_ThenThrowsE2eDocumentChangedException()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetIndexAsync("first", null);

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetIndexAsync("second", null));
        Assert.Equal("first", (await sut.GetIndexAsync())!.Value);
    }

    [Fact]
    public async Task GivenAnIndexChangedSinceRead_WhenSet_ThenThrowsE2eDocumentChangedException()
    {
        // Arrange
        var sut = CreateSut();
        var etag = await sut.SetIndexAsync("first", null);
        await sut.SetIndexAsync("other device", etag);

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => sut.SetIndexAsync("second", etag));
        Assert.Equal("other device", (await sut.GetIndexAsync())!.Value);
    }

    [Fact]
    public async Task GivenAStoredValueThatIsNotValid_WhenGot_ThenThrowsInvalidDataException()
    {
        // Arrange
        storage[LocalStorageUserKeysStore.IndexPrefix + "bob"] = "not json";
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.GetIndexAsync());
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
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(scope, null!, "1"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(scope, "keys", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryCreateAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetIndexAsync(null!, null));
    }

    private LocalStorageUserKeysStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object);
}
