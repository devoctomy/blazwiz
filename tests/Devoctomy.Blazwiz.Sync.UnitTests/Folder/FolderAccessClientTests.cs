using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync.FileTree;
using Devoctomy.Blazwiz.Sync.Folder;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Folder;

public class FolderAccessClientTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public FolderAccessClientTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == FolderAccessClient.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new FolderAccessClient(null!, mockKeys.Object)).ParamName);
    }

    [Fact]
    public async Task GivenBrowser_WhenIsSupportedAsync_ThenAsksModule()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("isSupported", It.IsAny<object?[]>())).ReturnsAsync(true);

        // Act & Assert
        Assert.True(await CreateSut().IsSupportedAsync());
    }

    [Fact]
    public async Task GivenUserPicks_WhenPickAsync_ThenReturnsFolderName()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string?>("pick", It.Is<object?[]>(a => (string?)a[0] == "bob"))).ReturnsAsync("Notes");

        // Act & Assert
        Assert.Equal("Notes", await CreateSut().PickAsync("bob"));
    }

    [Fact]
    public async Task GivenPickedFolder_WhenGetStatusAsync_ThenReturnsNameAndPermission()
    {
        // Arrange
        var status = new SyncFolderStatus { Name = "Notes", Permission = "prompt" };
        mockModule.Setup(x => x.InvokeAsync<SyncFolderStatus?>("getStatus", It.Is<object?[]>(a => (string?)a[0] == "bob"))).ReturnsAsync(status);

        // Act & Assert
        Assert.Same(status, await CreateSut().GetStatusAsync("bob"));
    }

    [Fact]
    public async Task GivenUserAllows_WhenReconnectAsync_ThenTrue()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("reconnect", It.Is<object?[]>(a => (string?)a[0] == "bob"))).ReturnsAsync(true);

        // Act & Assert
        Assert.True(await CreateSut().ReconnectAsync("bob"));
    }

    [Fact]
    public async Task GivenFiles_WhenListAsync_ThenListsUnderPrefix()
    {
        // Arrange
        var entries = new List<FileTreeEntry> { new() { Key = "notes/a.md" } };
        mockModule.Setup(x => x.InvokeAsync<List<FileTreeEntry>>("list", It.Is<object?[]>(a => (string?)a[0] == "bob" && (string?)a[1] == "notes/")))
            .ReturnsAsync(entries);

        // Act & Assert
        Assert.Same(entries, await CreateSut().ListAsync("bob", "notes/"));
    }

    [Fact]
    public async Task GivenFile_WhenReadAsync_ThenReadsIt()
    {
        // Arrange
        var file = new FileTreeFile { Content = [1] };
        mockModule.Setup(x => x.InvokeAsync<FileTreeFile?>("read", It.Is<object?[]>(a => (string?)a[0] == "bob" && (string?)a[1] == "notes/a.md"))).ReturnsAsync(file);

        // Act & Assert
        Assert.Same(file, await CreateSut().ReadAsync("bob", "notes/a.md"));
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenReturnsFileSystemTime()
    {
        // Arrange
        var content = new byte[] { 1 };
        mockModule.Setup(x => x.InvokeAsync<long>("write", It.Is<object?[]>(a => (string?)a[1] == "notes/a.md" && a[2] == content))).ReturnsAsync(42);

        // Act & Assert
        Assert.Equal(42, await CreateSut().WriteAsync("bob", "notes/a.md", content));
    }

    [Fact]
    public async Task GivenKey_WhenDeleteAsync_ThenRemovesIt()
    {
        // Act
        await CreateSut().DeleteAsync("bob", "notes/a.md");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("remove", It.Is<object?[]>(a => (string?)a[0] == "bob" && (string?)a[1] == "notes/a.md")), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.PickAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetStatusAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ReconnectAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ListAsync(" ", string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ListAsync("bob", null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.ReadAsync("bob", string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.WriteAsync("bob", string.Empty, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteAsync("bob", "k", null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeleteAsync("bob", string.Empty));
    }

    [Fact]
    public async Task GivenImportFailed_WhenCalledAgain_ThenImportRetried()
    {
        // Arrange
        mockJs.SetupSequence(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("offline"))
            .ReturnsAsync(mockModule.Object);
        var sut = CreateSut();

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
        var sut = CreateSut();
        await sut.IsSupportedAsync();

        // Act
        await sut.DisposeAsync();
        await CreateSut().DisposeAsync();

        // Assert
        mockModule.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private FolderAccessClient CreateSut() => new(mockJs.Object, mockKeys.Object);


    [Fact]
    public async Task GivenAppName_WhenModuleImported_ThenConfiguredWithTheAppPrefix()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("isSupported", It.IsAny<object?[]>())).ReturnsAsync(true);
        var sut = new FolderAccessClient(mockJs.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await sut.IsSupportedAsync();
        await sut.IsSupportedAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("configure", It.Is<object?[]>(a => (string?)a[0] == "app_")), Times.Once);
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new FolderAccessClient(mockJs.Object, null!)).ParamName);
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
