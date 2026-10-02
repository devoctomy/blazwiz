using blazwiz.Core.Sync.IndexedDb;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.IndexedDb;

public class IndexedDbClientTests
{
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
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new IndexedDbClient(null!)).ParamName);
    }

    [Fact]
    public async Task GivenKey_WhenGetAsync_ThenCallsModuleGet()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string?>("get", It.Is<object?[]>(a => a[0] == Store && (string?)a[1] == "k"))).ReturnsAsync("value");
        var sut = new IndexedDbClient(mockJs.Object);

        // Act & Assert
        Assert.Equal("value", await sut.GetAsync<string>(Store, "k"));
    }

    [Fact]
    public async Task GivenSeveralCalls_WhenUsingClient_ThenModuleImportedOnce()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object);

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
        var sut = new IndexedDbClient(mockJs.Object);

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
        mockModule.Setup(x => x.InvokeAsync<List<string>>("getAllByIndex", It.Is<object?[]>(a => a[0] == Store && (string?)a[1] == "owner" && (string?)a[2] == "bob" && ((IReadOnlyList<string>)a[3]!).Single() == "content")))
            .ReturnsAsync(["x"]);
        var sut = new IndexedDbClient(mockJs.Object);

        // Act & Assert
        Assert.Equal(["x"], await sut.GetAllByIndexAsync<string>(Store, "owner", "bob", ["content"]));
    }

    [Fact]
    public async Task GivenRange_WhenGetLastByIndexAsync_ThenCallsModule()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string?>("getLastByIndex", It.Is<object?[]>(a => (string?)a[1] == "i" && (string?)a[2] == "a" && (string?)a[3] == "z"))).ReturnsAsync("last");
        var sut = new IndexedDbClient(mockJs.Object);

        // Act & Assert
        Assert.Equal("last", await sut.GetLastByIndexAsync<string>(Store, "i", "a", "z"));
    }

    [Fact]
    public async Task GivenWrites_WhenCalled_ThenModuleFunctionsInvoked()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object);

        // Act
        await sut.PutManyAsync(Store, ["a", "b"]);
        await sut.DeleteManyAsync(Store, ["a"]);
        await sut.DeleteManyFromExistingAsync("db", "old", ["a"]);
        await sut.DeleteStoresAsync("db", ["old"]);

        // Assert
        VerifyVoid("putMany", a => ((string[])a[1]!).SequenceEqual(new[] { "a", "b" }));
        VerifyVoid("deleteMany", a => ((object[])a[1]!).Single() is "a");
        VerifyVoid("deleteManyFromExisting", a => (string?)a[0] == "db" && (string?)a[1] == "old");
        VerifyVoid("deleteStores", a => ((string[])a[1]!).Single() == "old");
    }

    [Fact]
    public async Task GivenExistingStoreQueries_WhenCalled_ThenModuleFunctionsInvoked()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("storeExists", It.Is<object?[]>(a => (string?)a[0] == "db" && (string?)a[1] == "old"))).ReturnsAsync(true);
        mockModule.Setup(x => x.InvokeAsync<List<string>>("getAllFromExisting", It.IsAny<object?[]>())).ReturnsAsync(["r"]);
        var sut = new IndexedDbClient(mockJs.Object);

        // Act & Assert
        Assert.True(await sut.StoreExistsAsync("db", "old"));
        Assert.Equal(["r"], await sut.GetAllFromExistingAsync<string>("db", "old"));
    }

    [Fact]
    public async Task GivenImportedModule_WhenDisposeAsync_ThenModuleDisposed()
    {
        // Arrange
        var sut = new IndexedDbClient(mockJs.Object);
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
}
