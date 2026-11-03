using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.JSInterop;
using Moq;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.ViewModels;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.ViewModels;

public class NoteEditorViewModelTests
{
    private readonly FrameworkText testText = new(typeof(SyncUiText));

    private static readonly DateTimeOffset SavedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<INoteService> mockNotes = new();
    private readonly Mock<IMarkdownRenderer> mockRenderer = new();
    private readonly Mock<IMarkdownFormatService> mockFormat = new();
    private readonly Mock<INoteEditorInteropService> mockEditor = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly NoteEntry note = new() { Id = "n1", Name = "todo.md", Kind = NoteEntryKind.Note };

    public NoteEditorViewModelTests()
    {
        mockRenderer.Setup(x => x.ToHtml(It.IsAny<string?>())).Returns<string?>(md => $"<p>{md}</p>");
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync(new NoteDocument { Entry = note, Content = "saved", FolderPath = ["Work"] });
        mockNotes.Setup(x => x.SaveNoteAsync("n1", It.IsAny<string>())).ReturnsAsync(() =>
        {
            note.ModifiedAt = SavedAt;
            return note;
        });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("noteService", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(null!, mockRenderer.Object, mockFormat.Object, mockEditor.Object, mockNavigation.Object, mockSync.Object, testText.Errors)).ParamName);
        Assert.Equal("markdownRenderer", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(mockNotes.Object, null!, mockFormat.Object, mockEditor.Object, mockNavigation.Object, mockSync.Object, testText.Errors)).ParamName);
        Assert.Equal("markdownFormatService", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(mockNotes.Object, mockRenderer.Object, null!, mockEditor.Object, mockNavigation.Object, mockSync.Object, testText.Errors)).ParamName);
        Assert.Equal("editorInteropService", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(mockNotes.Object, mockRenderer.Object, mockFormat.Object, null!, mockNavigation.Object, mockSync.Object, testText.Errors)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(mockNotes.Object, mockRenderer.Object, mockFormat.Object, mockEditor.Object, null!, mockSync.Object, testText.Errors)).ParamName);
    }

    [Fact]
    public void GivenNoteId_WhenGetEditUrl_ThenIdIsEscaped()
    {
        // Act & Assert
        Assert.Equal("files/edit?note=a%2Fb", NoteEditorViewModel.GetEditUrl("a/b"));
    }

    [Fact]
    public async Task GivenNote_WhenLoadAsync_ThenContentAndPreviewAreShownAndNotDirty()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LoadAsync("n1");

        // Assert
        Assert.False(sut.IsLoading);
        Assert.Same(note, sut.Note);
        Assert.Equal(["Work"], sut.FolderPath);
        Assert.Equal("saved", sut.Content);
        Assert.Equal("<p>saved</p>", sut.PreviewHtml);
        Assert.False(sut.IsDirty);
    }

    [Fact]
    public async Task GivenSameNote_WhenLoadAsyncAgain_ThenIsNotReloaded()
    {
        // Arrange
        var sut = CreateSut();
        await sut.LoadAsync("n1");
        sut.Content = "edited";

        // Act
        await sut.LoadAsync("n1");

        // Assert
        Assert.Equal("edited", sut.Content);
        mockNotes.Verify(x => x.GetNoteAsync("n1"), Times.Once);
    }

    [Fact]
    public async Task GivenMissingNote_WhenLoadAsync_ThenIsNotFound()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LoadAsync("missing");

        // Assert
        Assert.True(sut.IsNotFound);
        Assert.Null(sut.Note);
        Assert.False(sut.IsDirty);
    }

    [Fact]
    public async Task GivenStorageError_WhenLoadAsync_ThenShowsError()
    {
        // Arrange
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ThrowsAsync(new JSException("broken"));
        var sut = CreateSut();

        // Act
        await sut.LoadAsync("n1");

        // Assert
        Assert.Equal(FilesViewModel.StorageErrorMessage, sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task GivenFolderUnreachable_WhenLoadAsync_ThenShowsItsMessageWithoutCrashing()
    {
        // Arrange
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ThrowsAsync(new SyncStorageAccessException(SyncErrorCodes.FolderAccessNeeded, "Notes"));
        var sut = CreateSut();

        // Act
        await sut.LoadAsync("n1");

        // Assert
        Assert.Equal("Allow access to the folder 'Notes' to use your files.", sut.ErrorMessage);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task GivenFilesBeingMoved_WhenSaved_ThenShowsItsMessageAndStillDirty()
    {
        // Arrange
        mockNotes.Setup(x => x.SaveNoteAsync("n1", It.IsAny<string>())).ThrowsAsync(new SyncStorageMigrationException(SyncErrorCodes.StorageMigrating));
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Your files are being moved to another storage. Try again in a moment.", sut.ErrorMessage);
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public async Task GivenEdit_WhenContentChanges_ThenPreviewUpdatesAndIsDirty()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.Content = "changed";

        // Assert
        Assert.True(sut.IsDirty);
        Assert.Equal("<p>changed</p>", sut.PreviewHtml);
    }

    [Fact]
    public async Task GivenEdits_WhenSaved_ThenContentIsSavedAndNoLongerDirty()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.SaveNoteAsync("n1", "changed"), Times.Once);
        Assert.False(sut.IsDirty);
        Assert.False(sut.IsSaving);
        Assert.Equal(SavedAt, sut.LastSavedAt);
    }

    [Fact]
    public async Task GivenSaveFails_WhenSaved_ThenErrorIsShownAndStillDirty()
    {
        // Arrange
        mockNotes.Setup(x => x.SaveNoteAsync("n1", It.IsAny<string>())).ThrowsAsync(new NotesException("That note or folder no longer exists."));
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("That note or folder no longer exists.", sut.ErrorMessage);
        Assert.True(sut.IsDirty);
        sut.DismissErrorCommand.Execute(null);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenSelection_WhenFormat_ThenEditIsAppliedInEditorAndContent()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockEditor.Setup(x => x.GetSelectionAsync(NoteEditorViewModel.EditorElementId))
            .ReturnsAsync(new EditorSelection { Text = "say hello", Start = 4, End = 9 });
        var edit = new MarkdownTextEdit { Start = 4, End = 9, Replacement = "**hello**", SelectionStart = 6, SelectionEnd = 11 };
        mockFormat.Setup(x => x.Apply("say hello", 4, 9, MarkdownFormat.Bold)).Returns(edit);

        // Act
        await sut.FormatCommand.ExecuteAsync(MarkdownFormat.Bold);

        // Assert
        mockEditor.Verify(x => x.ApplyEditAsync(NoteEditorViewModel.EditorElementId, edit), Times.Once);
        Assert.Equal("say **hello**", sut.Content);
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public async Task GivenNoEditorOnPage_WhenFormat_ThenNothingHappens()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        await sut.FormatCommand.ExecuteAsync(MarkdownFormat.Bold);

        // Assert
        mockEditor.Verify(x => x.ApplyEditAsync(It.IsAny<string>(), It.IsAny<MarkdownTextEdit>()), Times.Never);
        mockFormat.Verify(x => x.Apply(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MarkdownFormat>()), Times.Never);
        Assert.Equal("saved", sut.Content);
    }

    [Fact]
    public void GivenPreviewShown_WhenTogglePreview_ThenCollapsesAndExpands()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.False(sut.IsPreviewCollapsed);
        sut.TogglePreviewCommand.Execute(null);
        Assert.True(sut.IsPreviewCollapsed);
        sut.TogglePreviewCommand.Execute(null);
        Assert.False(sut.IsPreviewCollapsed);
    }

    [Fact]
    public async Task GivenNoChanges_WhenCanLeave_ThenAllowed()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act & Assert
        Assert.True(sut.CanLeave("http://localhost/settings"));
        Assert.False(sut.IsLeaveDialogOpen);
    }

    [Fact]
    public async Task GivenUnsavedChanges_WhenCanLeave_ThenHeldAndDialogShown()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";

        // Act
        var result = sut.CanLeave("http://localhost/settings");

        // Assert
        Assert.False(result);
        Assert.True(sut.IsLeaveDialogOpen);
    }

    [Fact]
    public async Task GivenHeldNavigation_WhenStayOnPage_ThenDialogClosesWithoutNavigating()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";
        sut.CanLeave("http://localhost/settings");

        // Act
        sut.StayOnPageCommand.Execute(null);

        // Assert
        Assert.False(sut.IsLeaveDialogOpen);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        Assert.False(sut.CanLeave("http://localhost/settings"));
    }

    [Fact]
    public async Task GivenHeldNavigation_WhenDiscardAndLeave_ThenNavigatesWithoutSaving()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";
        sut.CanLeave("http://localhost/settings");

        // Act
        sut.DiscardAndLeaveCommand.Execute(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("http://localhost/settings"), Times.Once);
        mockNotes.Verify(x => x.SaveNoteAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.True(sut.CanLeave("http://localhost/settings"));
    }

    [Fact]
    public async Task GivenHeldNavigation_WhenSaveAndLeave_ThenSavesThenNavigates()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";
        sut.CanLeave("http://localhost/settings");

        // Act
        await sut.SaveAndLeaveCommand.ExecuteAsync(null);

        // Assert
        mockNotes.Verify(x => x.SaveNoteAsync("n1", "changed"), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("http://localhost/settings"), Times.Once);
        Assert.False(sut.IsLeaveDialogOpen);
    }

    [Fact]
    public async Task GivenSaveFails_WhenSaveAndLeave_ThenStaysOnPage()
    {
        // Arrange
        mockNotes.Setup(x => x.SaveNoteAsync("n1", It.IsAny<string>())).ThrowsAsync(new JSException("broken"));
        var sut = await CreateLoadedSutAsync();
        sut.Content = "changed";
        sut.CanLeave("http://localhost/settings");

        // Act
        await sut.SaveAndLeaveCommand.ExecuteAsync(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        Assert.False(sut.IsLeaveDialogOpen);
        Assert.Equal(FilesViewModel.StorageErrorMessage, sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenEditor_WhenClose_ThenNavigatesToFiles()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        sut.CloseCommand.Execute(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo(NoteEditorViewModel.FilesUrl), Times.Once);
    }

    [Fact]
    public void GivenNullSyncService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new NoteEditorViewModel(mockNotes.Object, mockRenderer.Object, mockFormat.Object, mockEditor.Object, mockNavigation.Object, null!, testText.Errors)).ParamName);
    }

    [Fact]
    public void GivenNoteKey_WhenGetEditUrl_ThenKeyIsEscapedInTheQueryString()
    {
        // Act & Assert
        Assert.Equal("files/edit?note=notes%2FWork%2FMy%20Ideas.md", NoteEditorViewModel.GetEditUrl("notes/Work/My Ideas.md"));
        Assert.Throws<ArgumentNullException>(() => NoteEditorViewModel.GetEditUrl(null!));
    }

    [Fact]
    public async Task GivenNoUnsavedEdits_WhenSyncChangesTheNote_ThenNewContentIsShown()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync(new NoteDocument { Entry = note, Content = "from another device" });

        // Act
        RaiseFilesChanged("n1");
        await WaitForAsync(() => sut.Content == "from another device");

        // Assert
        Assert.Equal("from another device", sut.Content);
        Assert.False(sut.IsDirty);
        Assert.False(sut.IsRemoteChangeDialogOpen);
    }

    [Fact]
    public async Task GivenNoUnsavedEdits_WhenSyncDeletesTheNote_ThenNotFoundWithMessage()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync((NoteDocument?)null);

        // Act
        RaiseFilesChanged("n1");
        await WaitForAsync(() => sut.IsNotFound);

        // Assert
        Assert.Null(sut.Note);
        Assert.Equal(NoteEditorViewModel.RemoteDeletedMessage, sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenUnsavedEdits_WhenSyncChangesTheNote_ThenAskedAndKeepMineKeepsEdits()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "my edit";
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync(new NoteDocument { Entry = note, Content = "theirs" });

        // Act
        RaiseFilesChanged("n1");
        await WaitForAsync(() => sut.IsRemoteChangeDialogOpen);
        sut.KeepMineCommand.Execute(null);

        // Assert
        Assert.False(sut.IsRemoteChangeDialogOpen);
        Assert.False(sut.IsRemoteDeleted);
        Assert.Equal("my edit", sut.Content);
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public async Task GivenUnsavedEdits_WhenTakeTheirs_ThenSyncedContentReplacesEdits()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "my edit";
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync(new NoteDocument { Entry = note, Content = "theirs" });
        RaiseFilesChanged("n1");
        await WaitForAsync(() => sut.IsRemoteChangeDialogOpen);

        // Act
        await sut.TakeTheirsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("theirs", sut.Content);
        Assert.False(sut.IsDirty);
    }

    [Fact]
    public async Task GivenUnsavedEdits_WhenNoteDeletedAndLetGo_ThenReturnsToFilesWithoutAsking()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();
        sut.Content = "my edit";
        mockNotes.Setup(x => x.GetNoteAsync("n1")).ReturnsAsync((NoteDocument?)null);
        RaiseFilesChanged("n1");
        await WaitForAsync(() => sut.IsRemoteChangeDialogOpen);

        // Act
        await sut.TakeTheirsCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsRemoteDeleted);
        mockNavigation.Verify(x => x.NavigateTo(NoteEditorViewModel.FilesUrl), Times.Once);
        Assert.True(sut.CanLeave("/anywhere"));
    }

    [Fact]
    public async Task GivenOtherFilesChanged_WhenSyncRuns_ThenEditorUntouched()
    {
        // Arrange
        var sut = await CreateLoadedSutAsync();

        // Act
        RaiseFilesChanged("other");
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("photos", ["n1"]));
        sut.Dispose();
        RaiseFilesChanged("n1");

        // Assert
        mockNotes.Verify(x => x.GetNoteAsync("n1"), Times.Once);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    private void RaiseFilesChanged(string key)
    {
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("notes", [key]));
    }

    private NoteEditorViewModel CreateSut() => new(mockNotes.Object, mockRenderer.Object, mockFormat.Object, mockEditor.Object, mockNavigation.Object, mockSync.Object, testText.Errors);

    private async Task<NoteEditorViewModel> CreateLoadedSutAsync()
    {
        var sut = CreateSut();
        await sut.LoadAsync("n1");
        return sut;
    }
}
