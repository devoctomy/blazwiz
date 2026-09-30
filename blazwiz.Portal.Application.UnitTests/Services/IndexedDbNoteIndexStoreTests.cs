using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Notes;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class IndexedDbNoteIndexStoreTests
{
    private readonly Mock<IJSRuntime> mockJsRuntime = new();

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new IndexedDbNoteIndexStore(null!)).ParamName);
    }

    [Fact]
    public async Task GivenOwner_WhenGetEntriesAsync_ThenReadsEntriesStoreByOwner()
    {
        // Arrange
        var entries = new List<NoteEntry> { new() { Id = "1" } };
        mockJsRuntime
            .Setup(x => x.InvokeAsync<List<NoteEntry>>("notesInterop.db.getAllByOwner", It.Is<object?[]>(a => (string?)a[0] == "entries" && (string?)a[1] == "bob")))
            .ReturnsAsync(entries);
        var sut = new IndexedDbNoteIndexStore(mockJsRuntime.Object);

        // Act
        var result = await sut.GetEntriesAsync("bob");

        // Assert
        Assert.Same(entries, result);
    }

    [Fact]
    public async Task GivenEntry_WhenSaveEntryAsync_ThenPutsIntoEntriesStore()
    {
        // Arrange
        var entry = new NoteEntry { Id = "1" };
        var sut = new IndexedDbNoteIndexStore(mockJsRuntime.Object);

        // Act
        await sut.SaveEntryAsync(entry);

        // Assert
        VerifyVoid("notesInterop.db.put", a => (string?)a[0] == "entries" && a[1] == entry);
    }

    [Fact]
    public async Task GivenIds_WhenDeleteEntriesAsync_ThenDeletesFromEntriesStore()
    {
        // Arrange
        var sut = new IndexedDbNoteIndexStore(mockJsRuntime.Object);

        // Act
        await sut.DeleteEntriesAsync(new List<string> { "1", "2" });

        // Assert
        VerifyVoid("notesInterop.db.deleteMany", a => (string?)a[0] == "entries" && ((string[])a[1]!).SequenceEqual(new[] { "1", "2" }));
    }

    [Fact]
    public async Task GivenState_WhenSavedAndRead_ThenUsesStateStore()
    {
        // Arrange
        var state = new NotesState { Owner = "bob", WelcomeNoteCreated = true };
        mockJsRuntime
            .Setup(x => x.InvokeAsync<NotesState?>("notesInterop.db.get", It.Is<object?[]>(a => (string?)a[0] == "state" && (string?)a[1] == "bob")))
            .ReturnsAsync(state);
        var sut = new IndexedDbNoteIndexStore(mockJsRuntime.Object);

        // Act
        await sut.SaveStateAsync(state);
        var result = await sut.GetStateAsync("bob");

        // Assert
        Assert.Same(state, result);
        VerifyVoid("notesInterop.db.put", a => (string?)a[0] == "state" && a[1] == state);
    }

    private void VerifyVoid(string identifier, Func<object?[], bool> argumentsMatch)
    {
        mockJsRuntime.Verify(
            x => x.InvokeAsync<IJSVoidResult>(identifier, It.Is<object?[]>(a => argumentsMatch(a))),
            Times.Once);
    }
}
