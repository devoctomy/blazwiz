using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync.FileTree;
using Devoctomy.Blazwiz.Sync.Opfs;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Opfs;

public class OpfsClientTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public OpfsClientTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == OpfsClient.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new OpfsClient(null!, mockKeys.Object)).ParamName);
    }

    [Fact]
    public async Task GivenBrowserSupport_WhenIsSupportedAsync_ThenReturnsIt()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("isSupported", It.IsAny<object?[]>())).ReturnsAsync(true);
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.True(await sut.IsSupportedAsync());
    }

    [Fact]
    public async Task GivenFiles_WhenListAsync_ThenCallsModuleList()
    {
        // Arrange
        var entries = new List<FileTreeEntry> { new() { Key = "notes/a.md", Size = 3, LastModified = 1 } };
        mockModule.Setup(x => x.InvokeAsync<List<FileTreeEntry>>("list", It.Is<object?[]>(a => (string?)a[0] == "root" && (string?)a[1] == "bob")))
            .ReturnsAsync(entries);
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.Same(entries, await sut.ListAsync("root", "bob"));
    }

    [Fact]
    public async Task GivenFile_WhenReadAsync_ThenCallsModuleRead()
    {
        // Arrange
        var file = new FileTreeFile { Content = [1], LastModified = 1 };
        mockModule.Setup(x => x.InvokeAsync<FileTreeFile?>("read", It.Is<object?[]>(a => (string?)a[0] == "root" && (string?)a[1] == "bob" && (string?)a[2] == "notes/a.md")))
            .ReturnsAsync(file);
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        Assert.Same(file, await sut.ReadAsync("root", "bob", "notes/a.md"));
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenCallsModuleWriteWithBytes()
    {
        // Arrange
        var content = new byte[] { 1, 2 };
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.WriteAsync("root", "bob", "notes/a.md", content);

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("write", It.Is<object?[]>(a => (string?)a[2] == "notes/a.md" && a[3] == content)), Times.Once);
    }

    [Fact]
    public async Task GivenKey_WhenDeleteAsync_ThenCallsModuleRemove()
    {
        // Arrange
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.DeleteAsync("root", "bob", "notes/a.md");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("remove", It.Is<object?[]>(a => (string?)a[2] == "notes/a.md")), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ListAsync(" ", "bob"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ListAsync("root", " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ReadAsync("root", "bob", string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.WriteAsync("root", "bob", string.Empty, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteAsync("root", "bob", "k", null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeleteAsync("root", "bob", string.Empty));
    }

    [Fact]
    public async Task GivenSeveralCalls_WhenUsingClient_ThenModuleImportedOnce()
    {
        // Arrange
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.IsSupportedAsync();
        await sut.IsSupportedAsync();

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenImportFailed_WhenCalledAgain_ThenImportRetried()
    {
        // Arrange
        mockJs.SetupSequence(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("offline"))
            .ReturnsAsync(mockModule.Object);
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act
        await Assert.ThrowsAsync<JSException>(() => sut.IsSupportedAsync());
        await sut.IsSupportedAsync();

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenImportedModule_WhenDisposeAsync_ThenModuleDisposedAndDisconnectIgnored()
    {
        // Arrange
        mockModule.Setup(x => x.DisposeAsync()).ThrowsAsync(new JSDisconnectedException("gone"));
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);
        await sut.IsSupportedAsync();

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenNothingImported_WhenDisposeAsync_ThenNothingHappens()
    {
        // Arrange
        var sut = new OpfsClient(mockJs.Object, mockKeys.Object);

        // Act
        await sut.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Never);
    }


    [Fact]
    public async Task GivenAppName_WhenListAsync_ThenRootIsNamedAfterTheApp()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<List<FileTreeEntry>>("list", It.IsAny<object?[]>())).ReturnsAsync([]);
        var sut = new OpfsClient(mockJs.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await sut.ListAsync("notes", "bob");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<List<FileTreeEntry>>("list", It.Is<object?[]>(a => (string?)a[0] == "app_notes" && (string?)a[1] == "bob")), Times.Once);
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new OpfsClient(mockJs.Object, null!)).ParamName);
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
