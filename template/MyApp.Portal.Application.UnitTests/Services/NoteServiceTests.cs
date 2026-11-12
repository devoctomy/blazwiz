using System.Text;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Moq;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.Services;

public class NoteServiceTests
{
    private const string Owner = "bob@example.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<INotesStateStore> mockState = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<INoteNameService> mockNames = new();
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly NoteTreeService tree = new();
    private Notebook notebook = new() { Name = NoteService.ProfileName, Id = Guid.NewGuid().ToString("D"), Root = NoteService.DefaultRoot, IsDefault = true };
    private readonly InMemorySyncFileStore files;
    private readonly Dictionary<string, NotesState> states = [];
    private SyncStatus syncStatus = new();
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.NoCredentials };

    public NoteServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = Owner });
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
        mockNames.Setup(x => x.Validate(It.IsAny<string?>())).Returns((string?)null);
        mockNames.Setup(x => x.ToNoteFileName(It.IsAny<string>())).Returns<string>(n => n.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? n : n + ".md");
        mockState.Setup(x => x.GetAsync(It.IsAny<string>())).ReturnsAsync((string o) => states.GetValueOrDefault(o));
        mockState.Setup(x => x.SaveAsync(It.IsAny<NotesState>())).Callback<NotesState>(s => states[s.Owner] = s).Returns(Task.CompletedTask);
        mockSync.SetupGet(x => x.Status).Returns(() => syncStatus);
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => connectivity);
        files = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("notebook", Assert.Throws<ArgumentNullException>(() => new NoteService(null!, files, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, null!, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("fileStore", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, null!, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("stateStore", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, null!, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, mockState.Object, null!, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, mockState.Object, mockSync.Object, null!, mockNames.Object, tree, mockClock.Object)).ParamName);
        Assert.Equal("noteNameService", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, null!, tree, mockClock.Object)).ParamName);
        Assert.Equal("noteTreeService", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, null!, mockClock.Object)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new NoteService(notebook, files, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, null!)).ParamName);
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
    public async Task GivenNewUserAndNoSyncPossible_WhenGetEntriesAsync_ThenWelcomeNoteCreatedStraightAwayAndOnce()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.GetEntriesAsync();
        var second = await sut.GetEntriesAsync();

        // Assert
        var welcome = Assert.Single(first);
        Assert.Equal("notes/Welcome.md", welcome.Id);
        Assert.Equal(NoteService.WelcomeNoteName, welcome.Name);
        Assert.Equal(Owner, welcome.Owner);
        Assert.Null(welcome.ParentId);
        Assert.Equal(NoteService.ReadWelcomeNote(), await ReadAsync("notes/Welcome.md"));
        Assert.Single(second);
        Assert.True(states[Owner].WelcomeNoteCreated);
    }

    [Fact]
    public async Task GivenACreatedNotebook_WhenGettingEntriesAndCreatingANote_ThenNoWelcomeNoteAndItsRootIsUsed()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        notebook = new Notebook { Name = id, Id = id, Root = $"profiles/{id}/" };
        await files.WriteAsync($"profiles/{id}/.notebook", Encoding.UTF8.GetBytes("{}"));
        var sut = CreateSut();

        // Act
        var before = await sut.GetEntriesAsync();
        var note = await sut.CreateNoteAsync("Page", null);

        // Assert
        Assert.Empty(before);
        Assert.Equal($"profiles/{id}/Page.md", note.Id);
        Assert.False(states.ContainsKey(Owner));
        Assert.Equal([note.Id], (await sut.GetEntriesAsync()).Select(e => e.Id));
    }

    [Fact]
    public async Task GivenOnlineBeforeFirstSync_WhenGetEntriesAsync_ThenWelcomeNoteWaitsForTheSync()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = true };
        var sut = CreateSut();

        // Act
        var beforeSync = await sut.GetEntriesAsync();
        var stateSavedBeforeSync = states.ContainsKey(Owner);
        syncStatus = new SyncStatus { LastSyncedAt = Now };
        var afterSync = await sut.GetEntriesAsync();

        // Assert
        Assert.Empty(beforeSync);
        Assert.False(stateSavedBeforeSync);
        Assert.Single(afterSync);
    }

    [Fact]
    public async Task GivenNotesSyncedFromAnotherDevice_WhenGetEntriesAsync_ThenNoWelcomeNoteButStateRecorded()
    {
        // Arrange
        syncStatus = new SyncStatus { LastSyncedAt = Now };
        await files.WriteAsync("notes/From another device.md", [1]);
        var sut = CreateSut();

        // Act
        var result = await sut.GetEntriesAsync();

        // Assert
        Assert.DoesNotContain(result, e => e.Name == NoteService.WelcomeNoteName);
        Assert.True(states[Owner].WelcomeNoteCreated);
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
    public async Task GivenFilesAndMarkers_WhenGetEntriesAsync_ThenTreeBuiltFromKeys()
    {
        // Arrange
        states[Owner] = new NotesState { Owner = Owner, WelcomeNoteCreated = true };
        await files.WriteAsync("notes/Work/Meetings/2026-10-01.md", [1]);
        await files.CreateFolderAsync("notes/Empty Folder/");
        await files.WriteAsync("notes/readme.txt", [1]);
        var sut = CreateSut();

        // Act
        var result = await sut.GetEntriesAsync();

        // Assert
        Assert.Equal(
            ["notes/Empty Folder/", "notes/Work/", "notes/Work/Meetings/", "notes/Work/Meetings/2026-10-01.md"],
            result.Select(e => e.Id).Order(StringComparer.Ordinal));
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
    public async Task GivenName_WhenCreateNoteAsync_ThenFileWrittenAtPathWithExtension()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);

        // Act
        var note = await sut.CreateNoteAsync("Plan", folder.Id, "# Plan");

        // Assert
        Assert.Equal("notes/Work/Plan.md", note.Id);
        Assert.Equal("Plan.md", note.Name);
        Assert.Equal("notes/Work/", note.ParentId);
        Assert.Equal(NoteEntryKind.Note, note.Kind);
        Assert.Equal(Now, note.ModifiedAt);
        Assert.Equal("# Plan", await ReadAsync("notes/Work/Plan.md"));
    }

    [Fact]
    public async Task GivenExistingName_WhenCreateNoteAsync_ThenThrowsNotesExceptionIgnoringCase()
    {
        // Arrange
        var sut = CreateSut();
        await sut.CreateNoteAsync("Ideas", null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync("IDEAS", null));
        Assert.Contains("IDEAS.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenSameNameInDifferentFolders_WhenCreateNoteAsync_ThenBothAreCreated()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);

        // Act
        await sut.CreateNoteAsync("Ideas", null);
        await sut.CreateNoteAsync("Ideas", folder.Id);

        // Assert
        Assert.NotNull(await files.ReadAsync("notes/Ideas.md"));
        Assert.NotNull(await files.ReadAsync("notes/Work/Ideas.md"));
    }

    [Fact]
    public async Task GivenInvalidName_WhenCreating_ThenThrowsNotesExceptionWithValidationError()
    {
        // Arrange
        mockNames.Setup(x => x.Validate("bad#")).Returns("Names cannot contain #");
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal("Names cannot contain #", (await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync("bad#", null))).Message);
        Assert.Equal("Names cannot contain #", (await Assert.ThrowsAsync<NotesException>(() => sut.CreateFolderAsync("bad#", null))).Message);
    }

    [Fact]
    public async Task GivenMissingOrNoteParent_WhenCreating_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("Ideas", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.CreateNoteAsync("x", "notes/Missing/"));
        await Assert.ThrowsAsync<NotesException>(() => sut.CreateFolderAsync("x", note.Id));
    }

    [Fact]
    public async Task GivenName_WhenCreateFolderAsync_ThenFolderMarkerWritten()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var folder = await sut.CreateFolderAsync(" Work ", null);

        // Assert
        Assert.Equal("notes/Work/", folder.Id);
        Assert.Equal("Work", folder.Name);
        Assert.True(folder.IsFolder);
        Assert.Empty((await files.ReadAsync("notes/Work/"))!.Content);
    }

    [Fact]
    public async Task GivenNote_WhenRenameAsync_ThenWrittenAtNewKeyAndOldKeyDeleted()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("Draft", null, "text");
        await files.WriteAsync(note.Id, Encoding.UTF8.GetBytes("text"), DateTimeOffset.UnixEpoch);

        // Act
        var renamed = await sut.RenameAsync(note.Id, "Final");

        // Assert
        Assert.Equal("notes/Final.md", renamed.Id);
        Assert.Equal("Final.md", renamed.Name);
        Assert.Null(await files.ReadAsync("notes/Draft.md"));
        var file = await files.ReadAsync("notes/Final.md");
        Assert.Equal("text", Encoding.UTF8.GetString(file!.Content));
        Assert.Equal(DateTimeOffset.UnixEpoch, file.ModifiedAt);
    }

    [Fact]
    public async Task GivenUnchangedName_WhenRenameAsync_ThenNothingWritten()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("Ideas", null);

        // Act
        var result = await sut.RenameAsync(note.Id, "Ideas");

        // Assert
        Assert.Equal(note.Id, result.Id);
    }

    [Fact]
    public async Task GivenOnlyCaseChange_WhenRenameAsync_ThenRenames()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("ideas", null);

        // Act
        var result = await sut.RenameAsync(note.Id, "Ideas");

        // Assert
        Assert.Equal("notes/Ideas.md", result.Id);
        Assert.Null(await files.ReadAsync("notes/ideas.md"));
    }

    [Fact]
    public async Task GivenSiblingWithName_WhenRenameAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        await sut.CreateNoteAsync("Taken", null);
        var note = await sut.CreateNoteAsync("Other", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.RenameAsync(note.Id, "taken"));
    }

    [Fact]
    public async Task GivenFolderWithContents_WhenRenameAsync_ThenEveryFileAndMarkerMoves()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        var meetings = await sut.CreateFolderAsync("Meetings", folder.Id);
        await sut.CreateNoteAsync("Agenda", meetings.Id, "agenda");

        // Act
        var renamed = await sut.RenameAsync(folder.Id, "Job");

        // Assert
        Assert.Equal("notes/Job/", renamed.Id);
        var keys = (await files.ListAsync()).Select(f => f.Key).Order(StringComparer.Ordinal);
        Assert.Equal(["notes/Job/", "notes/Job/Meetings/", "notes/Job/Meetings/Agenda.md"], keys);
        Assert.Equal("agenda", await ReadAsync("notes/Job/Meetings/Agenda.md"));
    }

    [Fact]
    public async Task GivenNote_WhenMoveAsync_ThenMovedIntoFolder()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Archive", null);
        var note = await sut.CreateNoteAsync("Old", null, "x");

        // Act
        var moved = await sut.MoveAsync(note.Id, folder.Id);

        // Assert
        Assert.Equal("notes/Archive/Old.md", moved.Id);
        Assert.Equal(folder.Id, moved.ParentId);
        Assert.Null(await files.ReadAsync("notes/Old.md"));
        Assert.Equal("x", await ReadAsync("notes/Archive/Old.md"));
    }

    [Fact]
    public async Task GivenSameParent_WhenMoveAsync_ThenNothingChanges()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("Ideas", null);

        // Act
        var result = await sut.MoveAsync(note.Id, null);

        // Assert
        Assert.Equal(note.Id, result.Id);
    }

    [Fact]
    public async Task GivenFolder_WhenMoveAsyncIntoItselfOrDescendant_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        var child = await sut.CreateFolderAsync("Child", folder.Id);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(folder.Id, child.Id));
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(folder.Id, folder.Id));
    }

    [Fact]
    public async Task GivenNameTakenInDestination_WhenMoveAsync_ThenThrowsNotesException()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        await sut.CreateNoteAsync("Ideas", folder.Id);
        var note = await sut.CreateNoteAsync("Ideas", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync(note.Id, folder.Id));
    }

    [Fact]
    public async Task GivenFolderWithContents_WhenDeleteAsync_ThenEverythingInsideIsDeleted()
    {
        // Arrange
        var sut = CreateSut();
        var folder = await sut.CreateFolderAsync("Work", null);
        await sut.CreateNoteAsync("Plan", folder.Id);
        await sut.CreateNoteAsync("Keep", null);

        // Act
        await sut.DeleteAsync(folder.Id);

        // Assert
        Assert.Equal(["notes/Keep.md"], (await files.ListAsync()).Select(f => f.Key));
    }

    [Fact]
    public async Task GivenMissingEntry_WhenDeleteAsync_ThenThrowsNotesException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => CreateSut().DeleteAsync("notes/missing.md"));
    }

    [Fact]
    public async Task GivenNestedNote_WhenGetNoteAsync_ThenReturnsContentAndFolderPath()
    {
        // Arrange
        await files.WriteAsync("notes/Work/Meetings/Agenda.md", Encoding.UTF8.GetBytes("# Agenda"), DateTimeOffset.UnixEpoch);

        // Act
        var result = await CreateSut().GetNoteAsync("notes/Work/Meetings/Agenda.md");

        // Assert
        Assert.Equal("# Agenda", result!.Content);
        Assert.Equal(["Work", "Meetings"], result.FolderPath);
        Assert.Equal("Agenda.md", result.Entry.Name);
        Assert.Equal(DateTimeOffset.UnixEpoch, result.Entry.ModifiedAt);
    }

    [Fact]
    public async Task GivenFolderOrMissingKey_WhenGetNoteAsync_ThenReturnsNull()
    {
        // Arrange
        await files.CreateFolderAsync("notes/Work/");
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(await sut.GetNoteAsync("notes/Work/"));
        Assert.Null(await sut.GetNoteAsync("notes/missing.md"));
        Assert.Null(await sut.GetNoteAsync("other/a.md"));
    }

    [Fact]
    public async Task GivenNote_WhenSaveNoteAsync_ThenContentWritten()
    {
        // Arrange
        var sut = CreateSut();
        var note = await sut.CreateNoteAsync("Ideas", null, "v1");

        // Act
        var result = await sut.SaveNoteAsync(note.Id, "v2");

        // Assert
        Assert.Equal("v2", await ReadAsync(note.Id));
        Assert.Equal(Now, result.ModifiedAt);
    }

    [Fact]
    public async Task GivenNoteDeletedElsewhere_WhenSaveNoteAsync_ThenItIsWrittenAgain()
    {
        // Act
        await CreateSut().SaveNoteAsync("notes/Kept.md", "mine");

        // Assert
        Assert.Equal("mine", await ReadAsync("notes/Kept.md"));
    }

    [Fact]
    public async Task GivenFolderKey_WhenSaveNoteAsync_ThenThrowsNotesException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => CreateSut().SaveNoteAsync("notes/Work/", "x"));
    }

    [Fact]
    public async Task GivenANoteDeletedWhileBeingMoved_WhenMoveAsync_ThenThrowsNotesExceptionAndNothingIsWrittenOrDeleted()
    {
        // Arrange
        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new SyncFileInfo { Key = "notes/Archive/", ModifiedAt = Now },
            new SyncFileInfo { Key = "notes/Old.md", ModifiedAt = Now },
        ]);
        store.Setup(x => x.ReadAsync("notes/Old.md", It.IsAny<CancellationToken>())).ReturnsAsync((SyncFile?)null);
        var sut = new NoteService(notebook, store.Object, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotesException>(() => sut.MoveAsync("notes/Old.md", "notes/Archive/"));
        Assert.Equal("That note or folder no longer exists.", ex.Message);
        store.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<string?> ReadAsync(string key)
    {
        var file = await files.ReadAsync(key);
        return file == null ? null : Encoding.UTF8.GetString(file.Content);
    }

    private NoteService CreateSut() =>
        new(notebook, files, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, tree, mockClock.Object);
}
