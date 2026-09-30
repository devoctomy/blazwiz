using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Notes;
using Microsoft.JSInterop;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class FilesViewModelTests
{
    private readonly Mock<INoteService> mockNotes = new();
    private readonly Mock<INavigationService> mockNavigation = new();
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
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("noteService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(null!, mockNavigation.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenEntries_WhenInitialized_ThenTopLevelIsShown()
    {
        // Arrange
        var sut = CreateSut();
        Assert.True(sut.IsLoading);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsLoading);
        Assert.Equal(["work", "todo"], sut.VisibleItems.Select(i => i.Entry.Id));
        Assert.Null(sut.SelectedFolderId);
        Assert.Equal(FilesViewModel.TopLevelName, sut.SelectedFolderName);
    }

    [Fact]
    public async Task GivenStorageUnavailable_WhenInitialized_ThenShowsError()
    {
        // Arrange
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(new JSException("IndexedDB unavailable"));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(FilesViewModel.StorageErrorMessage, sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task GivenFolder_WhenToggleFolder_ThenChildrenAreShownThenHidden()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act & Assert
        sut.ToggleFolderCommand.Execute("work");
        Assert.Equal(["work", "projects", "todo"], sut.VisibleItems.Select(i => i.Entry.Id));
        sut.ToggleFolderCommand.Execute("work");
        Assert.Equal(["work", "todo"], sut.VisibleItems.Select(i => i.Entry.Id));
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
        mockNavigation.Verify(x => x.NavigateTo("files/edit/todo"), Times.Once);
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
        mockNavigation.Verify(x => x.NavigateTo("files/edit/ideas"), Times.Once);
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
    public async Task GivenFolder_WhenShowMoveDialog_ThenTargetsExcludeItselfAndDescendants()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.ShowMoveDialogCommand.Execute("work");

        // Assert
        Assert.Equal(FilesDialogKind.Move, sut.ActiveDialog);
        Assert.Equal([FilesViewModel.TopLevelId], sut.MoveTargets.Select(t => t.Id));
        Assert.Equal(FilesViewModel.TopLevelId, sut.MoveTargetId);
    }

    [Fact]
    public async Task GivenMoveTarget_WhenConfirmingMove_ThenMovesAndExpandsDestination()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.ShowMoveDialogCommand.Execute("todo");
        Assert.Equal(["", "work", "projects"], sut.MoveTargets.Select(t => t.Id));

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

    private static NoteEntry Folder(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Folder };

    private static NoteEntry Note(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Note, StorageProvider = NoteStorageProviders.IndexedDb };

    private FilesViewModel CreateSut() => new(mockNotes.Object, mockNavigation.Object);

    private async Task<FilesViewModel> CreateLoadedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }
}
