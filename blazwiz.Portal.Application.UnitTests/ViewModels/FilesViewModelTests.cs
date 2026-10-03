using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
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
    private readonly Mock<INoteNameService> mockNames = new();
    private readonly Mock<INoteTreeService> mockTree = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<ISyncEncryptionService> mockEncryption = new();
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
        Assert.Equal("noteService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(null!, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockEncryption.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, null!, mockNames.Object, mockTree.Object, mockSync.Object, mockEncryption.Object)).ParamName);
        Assert.Equal("noteNameService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, mockNavigation.Object, null!, mockTree.Object, mockSync.Object, mockEncryption.Object)).ParamName);
        Assert.Equal("noteTreeService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, mockNavigation.Object, mockNames.Object, null!, mockSync.Object, mockEncryption.Object)).ParamName);
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
        await sut.OnInitializedAsync();

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
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(exception.Message, sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    public static TheoryData<Exception> StorageExceptions() => new()
    {
        new SyncStorageAccessException("Allow access to the folder 'Notes' to use your files."),
        new SyncStorageMigrationException("Your files are being moved to another storage. Try again in a moment."),
    };

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
    public void GivenNullSyncService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, null!, mockEncryption.Object)).ParamName);
        Assert.Equal("encryptionService", Assert.Throws<ArgumentNullException>(() => new FilesViewModel(mockNotes.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, null!)).ParamName);
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
    public void GivenEntries_WhenCanDecrypt_ThenNullForPlainAndKeyRingAnswerForEncrypted()
    {
        // Arrange
        mockEncryption.Setup(x => x.CanDecrypt("k_1")).Returns(true);
        mockEncryption.Setup(x => x.CanDecrypt("k_2")).Returns(false);
        var sut = CreateSut();

        // Act & Assert
        Assert.Null(sut.CanDecrypt(Note("plain", "plain.md", null)));
        Assert.True(sut.CanDecrypt(new NoteEntry { Id = "a", EncryptionKeyId = "k_1" }));
        Assert.False(sut.CanDecrypt(new NoteEntry { Id = "b", EncryptionKeyId = "k_2" }));
    }

    [Fact]
    public async Task GivenLoaded_WhenEncryptionStatusChanges_ThenEntriesReload()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockNotes.Invocations.Clear();

        // Act
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.Unlocked);

        // Assert
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
        sut.Dispose();
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.Disabled);
        mockNotes.Verify(x => x.GetEntriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenEncryptionLocked_WhenLoading_ThenErrorShown()
    {
        // Arrange
        mockNotes.Setup(x => x.GetEntriesAsync()).ThrowsAsync(new E2eLockedException("locked"));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("locked", sut.ErrorMessage);
    }

    private static NoteEntry Folder(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Folder };

    private static NoteEntry Note(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Note };

    private FilesViewModel CreateSut() => new(mockNotes.Object, mockNavigation.Object, mockNames.Object, mockTree.Object, mockSync.Object, mockEncryption.Object);

    private async Task<FilesViewModel> CreateLoadedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }
}
