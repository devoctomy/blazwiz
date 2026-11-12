using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.JSInterop;
using Moq;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.Sync;
using MyApp.Portal.Application.ViewModels;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.ViewModels;

public class FilesViewModelTests
{
    private readonly FrameworkText testText = new(typeof(SyncUiText));

    private readonly Mock<INoteService> mockNotes = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<INoteNameService> mockNames = new();
    private readonly Mock<INoteTreeService> mockTree = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<INotebookService> mockNotebooks = new();
    private readonly Mock<INoteServiceFactory> mockFactory = new();
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Notebook notes = new() { Name = "notes", Id = NotesSyncProfile.KeyId, Root = "notes/", Title = "Notes", IsDefault = true, HasPassphrase = true };
    private readonly E2eKeyScope notesScope = new(NotesSyncProfile.KeyId);
    private bool unlocked = true;
    private HashSet<string> lastExpandedFolderIds = [];
    private List<NoteEntry> entries =
    [
        Folder("work", "Work", null),
        Folder("projects", "Projects", "work"),
        Note("plan", "plan.md", "projects"),
        Note("todo", "todo.md", null),
    ];

    public FilesViewModelTests()
    {
        mockNotes.Setup(x => x.GetEntriesAsync()).ReturnsAsync(() => entries);
        mockSync.SetupGet(x => x.Status).Returns(new SyncStatus());
        mockManager.Setup(x => x.IsUnlocked(notesScope)).Returns(() => unlocked);
        mockNotebooks.Setup(x => x.FindAsync(It.IsAny<string?>())).ReturnsAsync((string? name) => name is null or "notes" ? notes : null);
        mockFactory.Setup(x => x.Create(notes)).Returns(mockNotes.Object);

        // The tree service decides which rows are visible; here every entry is returned, flagged as expanded when the
        // view model asked for it, and the expanded folders are captured so tests can check what was asked for.
        mockTree.Setup(x => x.BuildVisibleItems(It.IsAny<IEnumerable<NoteEntry>>(), It.IsAny<IReadOnlySet<string>>()))
            .Returns<IEnumerable<NoteEntry>, IReadOnlySet<string>>((all, expanded) =>
            {
                lastExpandedFolderIds = [.. expanded];
                return all.Select(e => new FileTreeItem { Entry = e, IsExpanded = expanded.Contains(e.Id) }).ToList();
            });
        mockTree.Setup(x => x.GetDescendantIds(It.IsAny<IEnumerable<NoteEntry>>(), It.IsAny<string>())).Returns(new HashSet<string>());
        mockTree.Setup(x => x.GetDescendantIds(It.IsAny<IEnumerable<NoteEntry>>(), "work")).Returns(new HashSet<string> { "projects", "plan" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("notebookService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(null!, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("noteServiceFactory", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, null!, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, null!, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, null!, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("noteNameService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, null!, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("noteTreeService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, null!, mockSync.Object, mockManager.Object, testText.Errors)).ParamName);
    }

    [Fact]
    public void GivenNameService_WhenGettingNameLimits_ThenComeFromNameService()
    {
        // Arrange
        mockNames.SetupGet(x => x.NoteExtension).Returns(".md");
        mockNames.SetupGet(x => x.MaxNameLength).Returns(42);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(".md", sut.NoteExtension);
        Assert.Equal(42, sut.MaxNameLength);
    }

    [Fact]
    public async Task GivenEntries_WhenInitialized_ThenTreeIsBuiltWithNothingExpanded()
    {
        // Arrange
        var sut = CreateSut();
        Assert.True(sut.IsLoading);

        // Act
        await sut.OpenNotebookAsync(null);

        // Assert
        Assert.False(sut.IsLoading);
        Assert.Equal(entries.Select(e => e.Id), sut.VisibleItems.Select(i => i.Entry.Id));
        Assert.Empty(lastExpandedFolderIds);
        Assert.Null(sut.SelectedFolderId);
        Assert.Equal(FilesViewModel.TopLevelName, sut.SelectedFolderName);
    }

    [Theory]
    [MemberData(nameof(StorageExceptions))]
    public async Task GivenFolderUnreachableOrFilesMoving_WhenInitialized_ThenShowsItsMessageWithoutCrashing(Exception exception)
    {
        // Arrange
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(exception);
        var sut = CreateSut();

        // Act
        await sut.OpenNotebookAsync(null);

        // Assert
        Assert.Equal(testText.Errors.Describe(exception), sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    public static TheoryData<Exception> StorageExceptions() => new()
    {
        new SyncStorageAccessException(SyncErrorCodes.FolderAccessNeeded, "Notes"),
        new SyncStorageMigrationException(SyncErrorCodes.StorageMigrating),
    };

    [Fact]
    public async Task GivenStorageUnavailable_WhenInitialized_ThenShowsError()
    {
        // Arrange
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(new JSException("IndexedDB unavailable"));
        var sut = CreateSut();

        // Act
        await sut.OpenNotebookAsync(null);

        // Assert
        Assert.Equal(FilesViewModel.StorageErrorMessage, sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task GivenFolder_WhenToggleFolder_ThenTreeIsRebuiltExpandedThenCollapsed()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act & Assert
        sut.ToggleFolderCommand.Execute("work");
        Assert.Equal(["work"], lastExpandedFolderIds);
        sut.ToggleFolderCommand.Execute("work");
        Assert.Empty(lastExpandedFolderIds);
    }

    [Fact]
    public async Task GivenFolder_WhenSelectFolder_ThenItIsSelectedAndExpanded()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.SelectFolderCommand.Execute("work");

        // Assert
        Assert.Equal("work", sut.SelectedFolderId);
        Assert.Equal("Work", sut.SelectedFolderName);
        Assert.True(sut.VisibleItems.Single(i => i.Entry.Id == "work").IsExpanded);
    }

    [Fact]
    public async Task GivenSelectedExpandedFolder_WhenSelectFolderAgain_ThenCollapsesAndSelectsTopLevel()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");

        // Act
        sut.SelectFolderCommand.Execute("work");

        // Assert
        Assert.Null(sut.SelectedFolderId);
        Assert.False(sut.VisibleItems.Single(i => i.Entry.Id == "work").IsExpanded);
    }

    [Fact]
    public async Task GivenNote_WhenOpenNote_ThenNavigatesToEditor()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.OpenNoteCommand.Execute("todo");

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("files/edit?note=todo"), Times.Once);
    }

    [Fact]
    public async Task GivenSelectedFolder_WhenCreatingNote_ThenCreatedThereAndOpened()
    {
        // Arrange
        mockNotes.Setup(x => x.CreateNoteAsync("ideas", "work", string.Empty)).ReturnsAsync(Note("ideas", "ideas.md", "work"));
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");

        // Act
        sut.ShowNewNoteDialogCommand.Execute(null);
        Assert.Equal(FilesDialogKind.NewNote, sut.ActiveDialog);
        sut.DialogName = "ideas";
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
        mockNavigation.Verify(x => x.NavigateTo("files/edit?note=ideas"), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidName_WhenConfirmingNewNote_ThenErrorIsShownAndDialogStaysOpen()
    {
        // Arrange
        mockNotes.Setup(x => x.CreateNoteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>())).ThrowsAsync(new NotesException("Please enter a name."));
        var sut = await CreateLoadedSutAsync();
        sut.ShowNewNoteDialogCommand.Execute(null);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(FilesDialogKind.NewNote, sut.ActiveDialog);
        Assert.Equal("Please enter a name.", sut.DialogError);
        Assert.False(sut.IsBusy);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenName_WhenCreatingFolder_ThenNewFolderIsSelectedAndListReloaded()
    {
        // Arrange
        mockNotes.Setup(x => x.CreateFolderAsync("Home", null)).ReturnsAsync(() =>
        {
            var folder = Folder("home", "Home", null);
            entries = [.. entries, folder];
            return folder;
        });
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowNewFolderDialogCommand.Execute(null);
        sut.DialogName = "Home";
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("home", sut.SelectedFolderId);
        Assert.Contains(sut.VisibleItems, i => i.Entry.Id == "home");
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
    }

    [Fact]
    public async Task GivenNote_WhenShowRenameDialog_ThenNameIsShownWithoutExtension()
    {
        // Arrange
        mockNames.Setup(x => x.WithoutExtension("todo.md")).Returns("todo");
        mockNotes.Setup(x => x.RenameAsync("todo", "done")).ReturnsAsync(Note("todo", "done.md", null));
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowRenameDialogCommand.Execute("todo");
        Assert.Equal("todo", sut.DialogName);
        sut.DialogName = "done";
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.RenameAsync("todo", "done"), Times.Once);
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
    }

    [Fact]
    public async Task GivenEntry_WhenShowMoveDialog_ThenTargetsComeFromTreeServiceAndCurrentFolderIsChosen()
    {
        // Arrange
        var targets = new List<FolderOption> { new() { Id = FilesViewModel.TopLevelId, Name = FilesViewModel.TopLevelName } };
        mockTree.Setup(x => x.BuildMoveTargets(It.IsAny<IEnumerable<NoteEntry>>(), "projects", FilesViewModel.TopLevelName)).Returns(targets);
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowMoveDialogCommand.Execute("projects");

        // Assert
        Assert.Equal(FilesDialogKind.Move, sut.ActiveDialog);
        Assert.Same(targets, sut.MoveTargets);
        Assert.Equal("work", sut.MoveTargetId);
    }

    [Fact]
    public async Task GivenMoveTarget_WhenConfirmingMove_ThenMovesAndExpandsDestination()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.ShowMoveDialogCommand.Execute("todo");

        // Act
        sut.MoveTargetId = "work";
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.MoveAsync("todo", "work"), Times.Once);
        Assert.True(sut.VisibleItems.Single(i => i.Entry.Id == "work").IsExpanded);
    }

    [Fact]
    public async Task GivenTopLevelTarget_WhenConfirmingMove_ThenMovesToNullParent()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.ShowMoveDialogCommand.Execute("projects");

        // Act
        sut.MoveTargetId = FilesViewModel.TopLevelId;
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.MoveAsync("projects", null), Times.Once);
    }

    [Fact]
    public async Task GivenFolderWithContents_WhenShowDeleteDialog_ThenCountsEverythingInside()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowDeleteDialogCommand.Execute("work");

        // Assert
        Assert.Equal(FilesDialogKind.Delete, sut.ActiveDialog);
        Assert.Equal(2, sut.DialogEntryDescendantCount);
    }

    [Fact]
    public async Task GivenSelectedFolder_WhenDeleted_ThenTopLevelIsSelected()
    {
        // Arrange
        mockNotes.Setup(x => x.DeleteAsync("work")).Callback(() => entries = entries.Where(e => e.Id == "todo").ToList()).Returns(Task.CompletedTask);
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("projects");

        // Act
        sut.ShowDeleteDialogCommand.Execute("work");
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.DeleteAsync("work"), Times.Once);
        Assert.Null(sut.SelectedFolderId);
        Assert.Equal(["todo"], sut.VisibleItems.Select(i => i.Entry.Id));
    }

    [Fact]
    public async Task GivenOpenDialog_WhenCloseDialog_ThenNothingChanges()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.ShowDeleteDialogCommand.Execute("todo");

        // Act
        sut.CloseDialogCommand.Execute(null);

        // Assert
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
        Assert.Null(sut.DialogEntry);
        mockNotes.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenDraggedNote_WhenDroppedOnFolder_ThenMovesIntoFolder()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.StartDragCommand.Execute("todo");
        sut.DragEnterCommand.Execute("work");
        Assert.Equal("work", sut.DropTargetId);
        await sut.DropCommand.ExecuteAsync("work");

        // Assert
        mockNotes.Verify(x => x.MoveAsync("todo", "work"), Times.Once);
        Assert.Null(sut.DraggedId);
        Assert.Null(sut.DropTargetId);
    }

    [Fact]
    public async Task GivenDraggedNote_WhenDroppedOnItsOwnFolder_ThenNothingHappens()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.StartDragCommand.Execute("todo");
        sut.DragEnterCommand.Execute(FilesViewModel.TopLevelId);
        Assert.Null(sut.DropTargetId);
        await sut.DropCommand.ExecuteAsync(FilesViewModel.TopLevelId);

        // Assert
        mockNotes.Verify(x => x.MoveAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenDraggedFolder_WhenDroppedOnItsDescendant_ThenNothingHappens()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.StartDragCommand.Execute("work");
        await sut.DropCommand.ExecuteAsync("projects");

        // Assert
        mockNotes.Verify(x => x.MoveAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenNestedFolder_WhenDroppedOnTopLevel_ThenMovesToTopLevel()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.StartDragCommand.Execute("projects");
        await sut.DropCommand.ExecuteAsync(FilesViewModel.TopLevelId);

        // Assert
        mockNotes.Verify(x => x.MoveAsync("projects", null), Times.Once);
    }

    [Fact]
    public async Task GivenMoveFails_WhenDropped_ThenErrorIsShown()
    {
        // Arrange
        mockNotes.Setup(x => x.MoveAsync("todo", "work")).ThrowsAsync(new NotesException("Name taken."));
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.StartDragCommand.Execute("todo");
        await sut.DropCommand.ExecuteAsync("work");

        // Assert
        Assert.Equal("Name taken.", sut.ErrorMessage);
        sut.DismissErrorCommand.Execute(null);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public void GivenNullSyncServiceOrManager_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, null!, mockManager.Object, testText.Errors)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenLoaded_WhenSyncChangesNotes_ThenTreeReloads()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        entries = [Note("new", "new.md", null)];

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["new"]));
        await WaitForAsync(() => sut.Entries.Count == 1);

        // Assert
        Assert.Equal("new", Assert.Single(sut.Entries).Id);
    }

    [Fact]
    public async Task GivenLoaded_WhenAnotherProfileChanges_ThenTreeNotReloaded()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("photos", ["a.png"]));

        // Assert
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenLoaded_WhenFirstSyncFinishes_ThenTreeReloadsOnce()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        var synced = new SyncStatus { LastSyncedAt = DateTimeOffset.UnixEpoch };

        // Act
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, synced);
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, synced);
        await WaitForAsync(() => mockNotes.Invocations.Count(i => i.Method.Name == nameof(INoteService.GetEntriesAsync)) >= 2);

        // Assert
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenDisposed_WhenSyncChangesNotes_ThenNothingReloads()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.Dispose();
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["x"]));

        // Assert
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task GivenAnUnknownNotebook_WhenOpened_ThenItIsMissingAndNothingIsLoaded()
    {
        // Arrange
        mockProfiles.SetupGet(x => x.IsLoaded).Returns(true);
        var sut = CreateSut();

        // Act
        await sut.OpenNotebookAsync("gone");

        // Assert
        Assert.True(sut.IsNotebookMissing);
        Assert.Null(sut.Notebook);
        Assert.False(sut.IsUnlocked);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenAnOpenNotebook_WhenLocked_ThenItIsLockedAndTheNotebooksPageIsShown()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.LockCommand.Execute(null);

        // Assert
        mockManager.Verify(x => x.Lock(notesScope), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("notebooks"), Times.Once);
    }

    [Fact]
    public async Task GivenTheNotebooksNotLoadedYet_WhenTheyLoad_ThenTheNotebookOpens()
    {
        // Arrange
        Notebook? found = null;
        mockNotebooks.Setup(x => x.FindAsync(null)).ReturnsAsync(() => found);
        var sut = CreateSut();
        await sut.OpenNotebookAsync(null);
        var missingBeforeLoad = sut.IsNotebookMissing;

        // Act
        found = notes;
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);
        await WaitForAsync(() => sut.Entries.Count > 0);

        // Assert
        Assert.False(missingBeforeLoad);
        Assert.Equal(notes, sut.Notebook);
        Assert.NotEmpty(sut.Entries);
    }

    [Fact]
    public async Task GivenACreatedNotebook_WhenANoteIsOpened_ThenTheEditorIsOpenedInThatNotebook()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        var notebook = new Notebook { Name = id, Id = id, Root = $"profiles/{id}/", Title = "Work", HasPassphrase = true };
        var pages = new Mock<INoteService>();
        pages.Setup(x => x.GetEntriesAsync()).ReturnsAsync([Note($"profiles/{id}/plan.md", "plan.md", null)]);
        mockNotebooks.Setup(x => x.FindAsync(id)).ReturnsAsync(notebook);
        mockFactory.Setup(x => x.Create(notebook)).Returns(pages.Object);
        mockManager.Setup(x => x.IsUnlocked(new E2eKeyScope(id))).Returns(true);
        var sut = CreateSut();
        await sut.OpenNotebookAsync(id);

        // Act
        sut.OpenNoteCommand.Execute($"profiles/{id}/plan.md");

        // Assert
        Assert.Equal("Work", sut.Notebook!.Title);
        Assert.Single(sut.Entries);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo($"files/edit?notebook={id}&note=profiles%2F{id}%2Fplan.md"), Times.Once);
    }

    [Fact]
    public async Task GivenNotesLocked_WhenInitialized_ThenNothingIsLoaded()
    {
        // Arrange
        unlocked = false;
        var sut = CreateSut();

        // Act
        await sut.OpenNotebookAsync(null);

        // Assert
        Assert.False(sut.IsUnlocked);
        Assert.Null(sut.ErrorMessage);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenNotesLocked_WhenUnlocked_ThenEntriesLoad()
    {
        // Arrange
        unlocked = false;
        var sut = await CreateLoadedSutAsync();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(Guid.NewGuid().ToString("D")));
        unlocked = true;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);
        await WaitForAsync(() => sut.VisibleItems.Count > 0);

        // Assert
        Assert.Contains(nameof(FilesViewModel.IsUnlocked), changed);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
        sut.Dispose();
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenEncryptionLocked_WhenLoading_ThenErrorShown()
    {
        // Arrange
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(new E2eLockedException());
        var sut = CreateSut();

        // Act
        await sut.OpenNotebookAsync(null);

        // Assert
        Assert.Equal("End to end encryption is locked on this device.", sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenAFolderSelected_WhenSelectTopLevel_ThenTheTopLevelIsSelected()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");

        // Act
        sut.SelectTopLevelCommand.Execute(null);

        // Assert
        Assert.Null(sut.SelectedFolderId);
        Assert.Equal(FilesViewModel.TopLevelName, sut.SelectedFolderName);
        Assert.Contains("work", lastExpandedFolderIds);
    }

    [Fact]
    public async Task GivenNoNotebookOpen_WhenOpenNote_ThenNavigatesToTheDefaultNotebooksEditor()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OpenNotebookAsync("gone");

        // Act
        sut.OpenNoteCommand.Execute("notes/a.md");

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("files/edit?note=notes%2Fa.md"), Times.Once);
    }

    [Fact]
    public async Task GivenNoNotebookOpen_WhenConfirmingANewNote_ThenThrowsAndIsNoLongerBusy()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OpenNotebookAsync("gone");
        sut.ShowNewNoteDialogCommand.Execute(null);
        sut.DialogName = "ideas";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(sut.ConfirmDialogAsync);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenFolder_WhenShowRenameDialog_ThenItsNameIsShownAsItIs()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowRenameDialogCommand.Execute("work");

        // Assert
        Assert.Equal(FilesDialogKind.Rename, sut.ActiveDialog);
        Assert.Equal("Work", sut.DialogName);
        mockNames.Verify(x => x.WithoutExtension(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenUnknownEntry_WhenShowingRenameMoveOrDeleteDialog_ThenNoDialogIsShown()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowRenameDialogCommand.Execute("gone");
        sut.ShowMoveDialogCommand.Execute("gone");
        sut.ShowDeleteDialogCommand.Execute("gone");

        // Assert
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
        Assert.Null(sut.DialogEntry);
    }

    [Fact]
    public async Task GivenNote_WhenShowDeleteDialog_ThenNothingInsideIsCounted()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        var countWithoutDialog = sut.DialogEntryDescendantCount;

        // Act
        sut.ShowDeleteDialogCommand.Execute("todo");

        // Assert
        Assert.Equal(0, countWithoutDialog);
        Assert.Equal(0, sut.DialogEntryDescendantCount);
    }

    [Fact]
    public async Task GivenSelectedFolder_WhenCreatingFolder_ThenItIsCreatedInsideTheExpandedFolderAndSelected()
    {
        // Arrange
        mockNotes.Setup(x => x.CreateFolderAsync("Sub", "work")).ReturnsAsync(() =>
        {
            var folder = Folder("sub", "Sub", "work");
            entries = [.. entries, folder];
            return folder;
        });
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");
        sut.ToggleFolderCommand.Execute("work");

        // Act
        sut.ShowNewFolderDialogCommand.Execute(null);
        sut.DialogName = "Sub";
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("sub", sut.SelectedFolderId);
        Assert.Equal("Sub", sut.SelectedFolderName);
        Assert.Contains("work", lastExpandedFolderIds);
        Assert.Equal(FilesDialogKind.None, sut.ActiveDialog);
    }

    [Fact]
    public async Task GivenStorageUnavailable_WhenConfirmingRename_ThenStorageErrorIsShownInTheDialog()
    {
        // Arrange
        mockNames.Setup(x => x.WithoutExtension("todo.md")).Returns("todo");
        mockNotes.Setup(x => x.RenameAsync("todo", "done")).ThrowsAsync(new JSException("IndexedDB unavailable"));
        var sut = await CreateLoadedSutAsync();
        sut.ShowRenameDialogCommand.Execute("todo");
        sut.DialogName = "done";

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(FilesDialogKind.Rename, sut.ActiveDialog);
        Assert.Equal(FilesViewModel.StorageErrorMessage, sut.DialogError);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenEncryptionLocked_WhenConfirmingDelete_ThenTheLockedErrorIsShownAndNothingIsRemoved()
    {
        // Arrange
        mockNotes.Setup(x => x.DeleteAsync("todo")).ThrowsAsync(new E2eLockedException());
        var sut = await CreateLoadedSutAsync();
        sut.ShowDeleteDialogCommand.Execute("todo");

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(FilesDialogKind.Delete, sut.ActiveDialog);
        Assert.Equal("End to end encryption is locked on this device.", sut.DialogError);
        Assert.Contains(sut.VisibleItems, i => i.Entry.Id == "todo");
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Theory]
    [MemberData(nameof(StorageExceptions))]
    public async Task GivenFolderUnreachableOrFilesMoving_WhenConfirmingNewFolder_ThenItsMessageIsShownInTheDialog(Exception exception)
    {
        // Arrange
        mockNotes.Setup(x => x.CreateFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ThrowsAsync(exception);
        var sut = await CreateLoadedSutAsync();
        sut.ShowNewFolderDialogCommand.Execute(null);
        sut.DialogName = "Home";

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(FilesDialogKind.NewFolder, sut.ActiveDialog);
        Assert.Equal(testText.Errors.Describe(exception), sut.DialogError);
    }

    [Fact]
    public async Task GivenAChangeBeingSaved_WhenConfirmedAgainOrSyncLockOrProfilesChange_ThenNothingElseRunsUntilItIsSaved()
    {
        // Arrange
        var created = new TaskCompletionSource<NoteEntry>();
        mockNotes.Setup(x => x.CreateFolderAsync("Home", null)).Returns(created.Task);
        var sut = await CreateLoadedSutAsync();
        sut.ShowNewFolderDialogCommand.Execute(null);
        sut.DialogName = "Home";
        var saving = sut.ConfirmDialogAsync();

        // Act
        await sut.ConfirmDialogAsync();
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["x"]));
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, new SyncStatus { LastSyncedAt = DateTimeOffset.UnixEpoch });
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);
        var busyWhileSaving = sut.IsBusy;
        var readsWhileSaving = mockNotes.Invocations.Count(i => i.Method.Name == nameof(INoteService.GetEntriesAsync));
        created.SetResult(Folder("home", "Home", null));
        await saving;

        // Assert
        Assert.True(busyWhileSaving);
        Assert.Equal(1, readsWhileSaving);
        Assert.False(sut.IsBusy);
        mockNotes.Verify(x => x.CreateFolderAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Exactly(2));
        mockFactory.Verify(x => x.Create(notes), Times.Once);
    }

    [Fact]
    public async Task GivenNothingDragged_WhenDropped_ThenNothingHappens()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        await sut.DropCommand.ExecuteAsync("work");

        // Assert
        mockNotes.Verify(x => x.MoveAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData("gone", "work")]
    [InlineData("todo", "plan")]
    [InlineData("todo", "missing")]
    [InlineData("work", "work")]
    public async Task GivenADragThatCannotBeDroppedThere_WhenDraggedOverAndDropped_ThenNotHighlightedAndNothingMoves(string draggedId, string targetId)
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.StartDragCommand.Execute(draggedId);

        // Act
        sut.DragEnterCommand.Execute(targetId);
        var dropTarget = sut.DropTargetId;
        await sut.DropCommand.ExecuteAsync(targetId);

        // Assert
        Assert.Null(dropTarget);
        Assert.Null(sut.DraggedId);
        mockNotes.Verify(x => x.MoveAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenSelectedFolder_WhenANoteElsewhereIsDeleted_ThenTheFolderStaysSelected()
    {
        // Arrange
        mockNotes.Setup(x => x.DeleteAsync("todo")).Callback(() => entries = entries.Where(e => e.Id != "todo").ToList()).Returns(Task.CompletedTask);
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");

        // Act
        sut.ShowDeleteDialogCommand.Execute("todo");
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.DeleteAsync("todo"), Times.Once);
        Assert.Equal("work", sut.SelectedFolderId);
        Assert.DoesNotContain(sut.VisibleItems, i => i.Entry.Id == "todo");
    }

    [Fact]
    public async Task GivenSelectedFolder_WhenSyncRemovesIt_ThenTheTopLevelIsSelected()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.SelectFolderCommand.Execute("work");
        entries = [Note("todo", "todo.md", null)];

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["work"]));
        await WaitForAsync(() => sut.Entries.Count == 1);

        // Assert
        Assert.Null(sut.SelectedFolderId);
        Assert.Empty(lastExpandedFolderIds);
    }

    [Fact]
    public async Task GivenLoaded_WhenABackgroundReloadFailsUnexpectedly_ThenThePageKeepsItsNotesWithoutAnError()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(new InvalidOperationException("unexpected"));

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["x"]));
        await WaitForAsync(() => mockNotes.Invocations.Count(i => i.Method.Name == nameof(INoteService.GetEntriesAsync)) >= 2);

        // Assert
        Assert.Null(sut.ErrorMessage);
        Assert.Equal(4, sut.Entries.Count);
    }

    [Fact]
    public async Task GivenNoNotebookOpen_WhenSyncOrLockStateChanges_ThenNothingIsLoaded()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OpenNotebookAsync("gone");

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", ["x"]));
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);

        // Assert
        Assert.Null(sut.Notebook);
        mockNotebooks.Verify(x => x.FindAsync(It.IsAny<string?>()), Times.Once);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenAnOpenNotebook_WhenItIsLockedElsewhere_ThenItIsShownLockedWithoutReloading()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        unlocked = false;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);

        // Assert
        Assert.False(sut.IsUnlocked);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenAnOpenNotebookNoLongerFound_WhenItIsUnlocked_ThenItStaysOpenAndReloads()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockNotebooks.Setup(x => x.FindAsync("notes")).ReturnsAsync((Notebook?)null);

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notesScope);
        await WaitForAsync(() => mockNotes.Invocations.Count(i => i.Method.Name == nameof(INoteService.GetEntriesAsync)) >= 2);

        // Assert
        Assert.Same(notes, sut.Notebook);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenAnOpenNotebookStillListed_WhenProfilesChange_ThenItIsNotOpenedAgain()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);

        // Assert
        Assert.Same(notes, sut.Notebook);
        mockFactory.Verify(x => x.Create(notes), Times.Once);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenTheNotebooksNotLoadedYet_WhenTheyLoadButFindingFails_ThenThePageDoesNotCrash()
    {
        // Arrange
        mockNotebooks.SetupSequence(x => x.FindAsync(null))
            .ReturnsAsync((Notebook?)null)
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        var sut = CreateSut();
        await sut.OpenNotebookAsync(null);

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);
        await WaitForAsync(() => mockNotebooks.Invocations.Count >= 2);

        // Assert
        Assert.Null(sut.Notebook);
        Assert.Null(sut.ErrorMessage);
        mockNotebooks.Verify(x => x.FindAsync(null), Times.Exactly(2));
    }

    private static NoteEntry Folder(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Folder };

    private static NoteEntry Note(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Note };

    private FilesViewModel CreateSut() => new(mockNotebooks.Object, mockFactory.Object, mockProfiles.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockManager.Object, testText.Errors);

    private async Task<FilesViewModel> CreateLoadedSutAsync()
    {
        var sut = CreateSut();
        await sut.OpenNotebookAsync(null);
        return sut;
    }
}
