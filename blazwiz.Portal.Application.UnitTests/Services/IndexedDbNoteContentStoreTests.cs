using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Notes;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class IndexedDbNoteContentStoreTests
{
    private readonly Mock<IJSRuntime> mockJsRuntime = new();

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new IndexedDbNoteContentStore(null!)).ParamName);
    }

    [Fact]
    public void GivenStore_WhenGettingProviderName_ThenIsIndexedDb()
    {
        Assert.Equal(NoteStorageProviders.IndexedDb, new IndexedDbNoteContentStore(mockJsRuntime.Object).ProviderName);
    }

    [Fact]
    public async Task GivenStoredContent_WhenReadAsync_ThenReturnsContent()
    {
        // Arrange
        mockJsRuntime
            .Setup(x => x.InvokeAsync<NoteContent?>("notesInterop.db.get", It.Is<object?[]>(a => (string?)a[0] == "content" && (string?)a[1] == "1")))
            .ReturnsAsync(new NoteContent { Id = "1", Content = "# Hi" });
        var sut = new IndexedDbNoteContentStore(mockJsRuntime.Object);

        // Act & Assert
        Assert.Equal("# Hi", await sut.ReadAsync("1"));
    }

    [Fact]
    public async Task GivenNoStoredContent_WhenReadAsync_ThenReturnsNull()
    {
        // Arrange
        var sut = new IndexedDbNoteContentStore(mockJsRuntime.Object);

        // Act & Assert
        Assert.Null(await sut.ReadAsync("1"));
    }

    [Fact]
    public async Task GivenContent_WhenWriteAsync_ThenPutsIntoContentStore()
    {
        // Arrange
        var sut = new IndexedDbNoteContentStore(mockJsRuntime.Object);

        // Act
        await sut.WriteAsync("1", "text");

        // Assert
        VerifyVoid("notesInterop.db.put", a => (string?)a[0] == "content" && a[1] is NoteContent { Id: "1", Content: "text" });
    }

    [Fact]
    public async Task GivenId_WhenDeleteAsync_ThenDeletesFromContentStore()
    {
        // Arrange
        var sut = new IndexedDbNoteContentStore(mockJsRuntime.Object);

        // Act
        await sut.DeleteAsync("1");

        // Assert
        VerifyVoid("notesInterop.db.deleteMany", a => (string?)a[0] == "content" && ((string[])a[1]!).SequenceEqual(new[] { "1" }));
    }

    private void VerifyVoid(string identifier, Func<object?[], bool> argumentsMatch)
    {
        mockJsRuntime.Verify(
            x => x.InvokeAsync<IJSVoidResult>(identifier, It.Is<object?[]>(a => argumentsMatch(a))),
            Times.Once);
    }
}
