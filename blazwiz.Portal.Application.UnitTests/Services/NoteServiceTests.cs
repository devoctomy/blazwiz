using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Notes;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NoteServiceTests
{
    private const string Owner = "bob@example.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ISystemClock> mockClock = new();
    private readonly InMemoryIndexStore indexStore = new();
    private readonly InMemoryContentStore contentStore = new();

    public NoteServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = Owner });
        mockClock.SetupGet(x => x.UtcNow).Returns(Now);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new NoteService(null!, indexStore, [contentStore], mockClock.Object)).ParamName);
        Assert.Equal("indexStore", Assert.Throws<ArgumentNullException>(() => new NoteService(mockAuth.Object, null!, [contentStore], mockClock.Object)).ParamName);
        Assert.Equal("contentStores", Assert.Throws<ArgumentNullException>(() => new NoteService(mockAuth.Object, indexStore, null!, mockClock.Object)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new NoteService(mockAuth.Object, indexStore, [contentStore], null!)).ParamName);
    }

    [Fact]
    public void GivenEmbeddedResource_WhenReadWelcomeNote_ThenReturnsMarkdown()
    {
        // Act
        var result = NoteService.ReadWelcomeNote();

        // Assert
        Assert.StartsWith("# Welcome", result);
        Assert.DoesNotContain("\r", result);
    }

    [Fact]
    public async Task GivenNewUser_WhenGetEntriesAsync_ThenWelcomeNoteIsCreatedOnce()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.GetEntriesAsync();
        var second = await sut.GetEntriesAsync();

        // Assert
        var welcome = Assert.Single(first);
        Assert.Equal(NoteService.WelcomeNoteName, welcome.Name);
        Assert.Equal(Owner, welcome.Owner);
        Assert.Equal(NoteStorageProviders.IndexedDb, welcome.StorageProvider);
        Assert.Null(welcome.ParentId);
        Assert.Equal(NoteService.ReadWelcomeNote(), contentStore.Content[welcome.Id]);
        Assert.Single(second);
        Assert.True(indexStore.States[Owner].WelcomeNoteCreated);
    }

    [Fact]
    public async Task GivenWelcomeNoteDeleted_WhenGetEntriesAsync_ThenItIsNotCreatedAgain()
    {
        // Arrange
        var sut = CreateSut();
        var welcome = Assert.Single(await sut.GetEntriesAsync());

        // Act
        await sut.DeleteAsync(welcome.Id);
        var result = await sut.GetEntriesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GivenOtherUsersNotes_WhenGetEntriesAsync_ThenOnlyOwnNotesAreReturned()
    {
        // Arrange
        indexStore.States["alice"] = new NotesState { Owner = "alice", WelcomeNoteCreated = true };
        indexStore.Entries["x"] = new NoteEntry { Id = "x", Owner = "alice", Name = "secret.md", Kind = NoteEntryKind.Note };
        var sut = CreateSut();

        // Act
        var result = await sut.GetEntriesAsync();

        // Assert
        Assert.DoesNotContain(result, e => e.Id == "x");
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetEntriesAsync_ThenThrowsNotesException()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(sut.GetEntriesAsync);
    }

    [Fact]
    public async Task GivenName_WhenCreateNoteAsync_ThenNoteAndContentAreSavedWithExtension()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var note = await sut.CreateNoteAsync("  Shopping  ", null, "- milk");

        // Assert
        Assert.Equal("Shopping.md", note.Name);
        Assert.Equal(NoteEntryKind.Note, note.Kind);
        Assert.Equal(Now, note.CreatedAt);
        Assert.Equal(Now, note.ModifiedAt);
        Assert.Same(note, indexStore.Entries[note.Id]);
        Assert.Equal("- milk", contentStore.Content[note.Id]);
    }

    [Fact]
    public async Task GivenExistingName_WhenCreateNoteAsync_ThenThrowsNotesExceptionIgnoringCase()
    {
        // Arrange
        var sut = CreateSut();
        await sut.CreateNoteAsync("todo", null);

        // Act
        var exception = await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync("TODO.md", null));

        // Assert
        Assert.Contains("already something called 'TODO.md'", exception.Message);
    }

    [Fact]
    public async Task GivenSameNameInDifferentFolders_WhenCreateNoteAsync_ThenBothAreCreated()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);

        // Act
        await sut.CreateNoteAsync("todo", null);
        await sut.CreateNoteAsync("todo", folder.Id);

        // Assert
        Assert.Equal(2, indexStore.Entries.Values.Count(e => e.Name == "todo.md"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    public async Task GivenInvalidName_WhenCreateNoteAsync_ThenThrowsNotesException(string name)
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync(name, null));
        Assert.Empty(contentStore.Content);
    }

    [Fact]
    public async Task GivenMissingParent_WhenCreateNoteAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync("todo", "missing"));
    }

    [Fact]
    public async Task GivenNoteAsParent_WhenCreateFolderAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("todo", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.CreateFolderAsync("Inside", note.Id));
    }

    [Fact]
    public async Task GivenName_WhenCreateFolderAsync_ThenFolderIsSavedWithoutExtension()
    {
        // Arrange
        var sut = CreateSut();
        var parent = await sut.CreateFolderAsync("Work", null);

        // Act
        var folder = await sut.CreateFolderAsync(" Projects ", parent.Id);

        // Assert
        Assert.Equal("Projects", folder.Name);
        Assert.Equal(NoteEntryKind.Folder, folder.Kind);
        Assert.Equal(parent.Id, folder.ParentId);
        Assert.Null(folder.StorageProvider);
    }

    [Fact]
    public async Task GivenNote_WhenRenameAsync_ThenNameKeepsExtension()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("todo", null);
        var later = Now.AddMinutes(5);
        mockClock.SetupGet(x => x.UtcNow).Returns(later);

        // Act
        var result = await sut.RenameAsync(note.Id, "done");

        // Assert
        Assert.Equal("done.md", result.Name);
        Assert.Equal(later, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenSiblingWithName_WhenRenameAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        await sut.CreateFolderAsync("Work", null);
        var folder = await sut.CreateFolderAsync("Home", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.RenameAsync(folder.Id, "work"));
    }

    [Fact]
    public async Task GivenOnlyCaseChange_WhenRenameAsync_ThenRenames()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("work", null);

        // Act
        var result = await sut.RenameAsync(folder.Id, "Work");

        // Assert
        Assert.Equal("Work", result.Name);
    }

    [Fact]
    public async Task GivenNote_WhenMoveAsync_ThenParentChanges()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        var note = await sut.CreateNoteAsync("todo", null);

        // Act & Assert
        Assert.Equal(folder.Id, (await sut.MoveAsync(note.Id, folder.Id)).ParentId);
        Assert.Equal(folder.Id, indexStore.Entries[note.Id].ParentId);
        Assert.Null((await sut.MoveAsync(note.Id, null)).ParentId);
    }

    [Fact]
    public async Task GivenFolder_WhenMoveAsyncIntoItsOwnDescendant_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var work = await sut.CreateFolderAsync("Work", null);
        var projects = await sut.CreateFolderAsync("Projects", work.Id);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(work.Id, projects.Id));
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(work.Id, work.Id));
    }

    [Fact]
    public async Task GivenNameTakenInDestination_WhenMoveAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        await sut.CreateNoteAsync("todo", folder.Id);
        var note = await sut.CreateNoteAsync("todo", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(note.Id, folder.Id));
    }

    [Fact]
    public async Task GivenFolderWithContents_WhenDeleteAsync_ThenEverythingInsideAndItsContentIsDeleted()
    {
        // Arrange
        var sut = CreateSut();
        var work = await sut.CreateFolderAsync("Work", null);
        var projects = await sut.CreateFolderAsync("Projects", work.Id);
        var plan = await sut.CreateNoteAsync("plan", projects.Id, "plan");
        var keep = await sut.CreateNoteAsync("keep", null, "keep");

        // Act
        await sut.DeleteAsync(work.Id);

        // Assert
        Assert.Equal([keep.Id], indexStore.Entries.Keys);
        Assert.False(contentStore.Content.ContainsKey(plan.Id));
        Assert.True(contentStore.Content.ContainsKey(keep.Id));
    }

    [Fact]
    public async Task GivenMissingEntry_WhenDeleteAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.DeleteAsync("missing"));
    }

    [Fact]
    public async Task GivenNestedNote_WhenGetNoteAsync_ThenReturnsContentAndFolderPath()
    {
        // Arrange
        var sut = CreateSut();
        var work = await sut.CreateFolderAsync("Work", null);
        var projects = await sut.CreateFolderAsync("Projects", work.Id);
        var plan = await sut.CreateNoteAsync("plan", projects.Id, "# Plan");

        // Act
        var result = await sut.GetNoteAsync(plan.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Same(plan, result.Entry);
        Assert.Equal("# Plan", result.Content);
        Assert.Equal(["Work", "Projects"], result.FolderPath);
    }

    [Fact]
    public async Task GivenFolderOrMissingId_WhenGetNoteAsync_ThenReturnsNull()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);

        // Act & Assert
        Assert.Null(await sut.GetNoteAsync(folder.Id));
        Assert.Null(await sut.GetNoteAsync("missing"));
    }

    [Fact]
    public async Task GivenNoteFromUnavailableProvider_WhenGetNoteAsync_ThenThrowsNotesException()
    {
        // Arrange
        indexStore.Entries["x"] = new NoteEntry { Id = "x", Owner = Owner, Name = "x.md", Kind = NoteEntryKind.Note, StorageProvider = "opfs" };
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<NotesException>(() => sut.GetNoteAsync("x"));

        // Assert
        Assert.Contains("opfs", exception.Message);
    }

    [Fact]
    public async Task GivenNote_WhenSaveNoteAsync_ThenContentAndModifiedTimeAreUpdated()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("todo", null, "old");
        var later = Now.AddHours(1);
        mockClock.SetupGet(x => x.UtcNow).Returns(later);

        // Act
        var result = await sut.SaveNoteAsync(note.Id, "new");

        // Assert
        Assert.Equal("new", contentStore.Content[note.Id]);
        Assert.Equal(later, result.ModifiedAt);
        Assert.Equal(later, indexStore.Entries[note.Id].ModifiedAt);
    }

    [Fact]
    public async Task GivenFolder_WhenSaveNoteAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.SaveNoteAsync(folder.Id, "text"));
    }

    private NoteService CreateSut()
    {
        return new NoteService(mockAuth.Object, indexStore, [contentStore], mockClock.Object);
    }

    private sealed class InMemoryIndexStore : INoteIndexStore
    {
        public Dictionary<string, NoteEntry> Entries { get; } = [];

        public Dictionary<string, NotesState> States { get; } = [];

        public Task<IReadOnlyList<NoteEntry>> GetEntriesAsync(string owner) =>
            Task.FromResult<IReadOnlyList<NoteEntry>>(Entries.Values.Where(e => e.Owner == owner).ToList());

        public Task SaveEntryAsync(NoteEntry entry)
        {
            Entries[entry.Id] = entry;
            return Task.CompletedTask;
        }

        public Task DeleteEntriesAsync(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                Entries.Remove(id);
            }

            return Task.CompletedTask;
        }

        public Task<NotesState?> GetStateAsync(string owner) => Task.FromResult(States.GetValueOrDefault(owner));

        public Task SaveStateAsync(NotesState state)
        {
            States[state.Owner] = state;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryContentStore : INoteContentStore
    {
        public Dictionary<string, string> Content { get; } = [];

        public string ProviderName => NoteStorageProviders.IndexedDb;

        public Task<string?> ReadAsync(string id) => Task.FromResult(Content.GetValueOrDefault(id));

        public Task WriteAsync(string id, string content)
        {
            Content[id] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            Content.Remove(id);
            return Task.CompletedTask;
        }
    }
}
