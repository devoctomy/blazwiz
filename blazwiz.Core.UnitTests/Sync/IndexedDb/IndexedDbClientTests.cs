using blazwiz.Core.Configuration;
using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Sync.IndexedDb;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.IndexedDb;

public class IndexedDbClientTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private static readonly IndexedDbStore Store = new("db", "store", ["id"]);

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public IndexedDbClientTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == IndexedDbClient.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new IndexedDbClient(null!, mockKeys.Object)).ParamName);
    }

    [Fact]
    public async Task GivenKey_WhenGetAsync_ThenCallsModuleGet()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string?>("get", It.Is<object?[]>(a => ((IndexedDbStore)a[0]!).Database == Store.Database && ((IndexedDbStore)a[0]!).Name == Store.Name && (string?)a[1] == "k"))).ReturnsAsync("value");
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.Equal("value", await sut.GetAsync<string>(Store, "k"));
    }

    [Fact]
    public async Task GivenSeveralCalls_WhenUsingClient_ThenModuleImportedOnce()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.PutAsync(Store, "a");
        await sut.PutAsync(Store, "b");

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenImportFails_WhenCalledAgain_ThenImportIsRetried()
    {
        // Arrange
        mockJs.SetupSequence(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("offline"))
            .ReturnsAsync(mockModule.Object);
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act
        await Assert.ThrowsAsync<JSException>(() => sut.PutAsync(Store, "a"));
        await sut.PutAsync(Store, "a");

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenIndexQuery_WhenGetAllByIndexAsync_ThenPassesOmitFields()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<List<string>>("getAllByIndex", It.Is<object?[]>(a => ((IndexedDbStore)a[0]!).Database == Store.Database && ((IndexedDbStore)a[0]!).Name == Store.Name && (string?)a[1] == "owner" && (string?)a[2] == "bob" && ((IReadOnlyList<string>)a[3]!).Single() == "content")))
            .ReturnsAsync(["x"]);
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.Equal(["x"], await sut.GetAllByIndexAsync<string>(Store, "owner", "bob", ["content"]));
    }

    [Fact]
    public async Task GivenRange_WhenGetLastByIndexAsync_ThenCallsModule()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string?>("getLastByIndex", It.Is<object?[]>(a => (string?)a[1] == "i" && (string?)a[2] == "a" && (string?)a[3] == "z"))).ReturnsAsync("last");
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.Equal("last", await sut.GetLastByIndexAsync<string>(Store, "i", "a", "z"));
    }

    [Fact]
    public async Task GivenWrites_WhenCalled_ThenModuleFunctionsInvoked()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.PutManyAsync(Store, ["a", "b"]);
        await sut.DeleteManyAsync(Store, ["a"]);

        // Assert
        VerifyVoid("putMany", a => ((string[])a[1]!).SequenceEqual(new[] { "a", "b" }));
        VerifyVoid("deleteMany", a => ((object[])a[1]!).Single() is "a");
    }

    [Fact]
    public async Task GivenImportedModule_WhenDisposeAsync_ThenModuleDisposed()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object, mockKeys.Object);
        await sut.PutAsync(Store, "a");

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private void VerifyVoid(string identifier, Func<object?[], bool> args)
    {
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>(identifier, It.Is<object?[]>(a => args(a))), Times.Once);
    }


    [Fact]
    public async Task GivenAppName_WhenCalled_ThenDatabaseIsNamedAfterTheApp()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await sut.PutAsync(Store, "a");

        // Assert
        VerifyVoid("put", a => a[0] is IndexedDbStore s && s.Database == "app_db" && s.Name == Store.Name && s.KeyPath.SequenceEqual(Store.KeyPath));
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new IndexedDbClient(mockJs.Object, null!)).ParamName);
    }

    // Keeps names as they are, so the tests can check them; the prefix itself is checked separately.
    private static Mock<IAppStorageKeys> CreateKeys()
    {
        var keys = new Mock<IAppStorageKeys>();
        keys.Setup(x => x.For(It.IsAny<string>())).Returns<string>(name => name);
        keys.SetupGet(x => x.Prefix).Returns(string.Empty);
        return keys;
    }
}
