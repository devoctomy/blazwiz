using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;
using CommunityToolkit.Mvvm.Input;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the note editor: markdown text with formatting buttons, a live preview and saving.
/// </summary>
public partial class NoteEditorViewModel : ViewModelBase
{
    /// <summary>
    /// The id of the editor's textarea.
    /// </summary>
    public const string EditorElementId = "note-editor";

    /// <summary>
    /// The URL of the files page.
    /// </summary>
    public const string FilesUrl = "files";

    private readonly INoteService noteService;
    private readonly IMarkdownRenderer markdownRenderer;
    private readonly INoteEditorInteropService editorInteropService;
    private readonly INavigationService navigationService;

    private string? loadedNoteId;
    private NoteEntry? note;
    private IReadOnlyList<string> folderPath = [];
    private string content = string.Empty;
    private string savedContent = string.Empty;
    private string previewHtml = string.Empty;
    private bool isLoading = true;
    private bool isSaving;
    private bool isNotFound;
    private bool isPreviewCollapsed;
    private bool isLeaveDialogOpen;
    private bool leaveConfirmed;
    private string? pendingLocation;
    private string? errorMessage;
    private DateTimeOffset? lastSavedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteEditorViewModel"/> class.
    /// </summary>
    /// <param name="noteService">The note service.</param>
    /// <param name="markdownRenderer">The markdown renderer, for the preview.</param>
    /// <param name="editorInteropService">Access to the editor's text and selection in the browser.</param>
    /// <param name="navigationService">The navigation service.</param>
    public NoteEditorViewModel(
        INoteService noteService,
        IMarkdownRenderer markdownRenderer,
        INoteEditorInteropService editorInteropService,
        INavigationService navigationService)
    {
        this.noteService = ValidationHelpers.VerifiedAssignment(noteService);
        this.markdownRenderer = ValidationHelpers.VerifiedAssignment(markdownRenderer);
        this.editorInteropService = ValidationHelpers.VerifiedAssignment(editorInteropService);
        this.navigationService = ValidationHelpers.VerifiedAssignment(navigationService);
    }

    /// <summary>
    /// Gets the note being edited, or null until it has loaded.
    /// </summary>
    public NoteEntry? Note
    {
        get => note;
        private set => SetProperty(ref note, value);
    }

    /// <summary>
    /// Gets the names of the folders containing the note, outermost first.
    /// </summary>
    public IReadOnlyList<string> FolderPath
    {
        get => folderPath;
        private set => SetProperty(ref folderPath, value);
    }

    /// <summary>
    /// Gets or sets the markdown being edited. Setting it updates the preview.
    /// </summary>
    public string Content
    {
        get => content;
        set
        {
            if (SetProperty(ref content, value ?? string.Empty))
            {
                PreviewHtml = markdownRenderer.ToHtml(content);
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    /// <summary>
    /// Gets the preview of <see cref="Content"/> as HTML. Raw HTML in the markdown is escaped, so it is safe to display.
    /// </summary>
    public string PreviewHtml
    {
        get => previewHtml;
        private set => SetProperty(ref previewHtml, value);
    }

    /// <summary>
    /// Gets a value indicating whether there are unsaved changes.
    /// </summary>
    public bool IsDirty => Note != null && Content != savedContent;

    /// <summary>
    /// Gets a value indicating whether the note is loading.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>
    /// Gets a value indicating whether the note is being saved.
    /// </summary>
    public bool IsSaving
    {
        get => isSaving;
        private set => SetProperty(ref isSaving, value);
    }

    /// <summary>
    /// Gets a value indicating whether the user has no note with the requested id.
    /// </summary>
    public bool IsNotFound
    {
        get => isNotFound;
        private set => SetProperty(ref isNotFound, value);
    }

    /// <summary>
    /// Gets a value indicating whether the preview pane is collapsed.
    /// </summary>
    public bool IsPreviewCollapsed
    {
        get => isPreviewCollapsed;
        private set => SetProperty(ref isPreviewCollapsed, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog asking what to do with unsaved changes is showing.
    /// </summary>
    public bool IsLeaveDialogOpen
    {
        get => isLeaveDialogOpen;
        private set => SetProperty(ref isLeaveDialogOpen, value);
    }

    /// <summary>
    /// Gets the last error, e.g. a failed save.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Gets when the note was last saved in this editing session.
    /// </summary>
    public DateTimeOffset? LastSavedAt
    {
        get => lastSavedAt;
        private set => SetProperty(ref lastSavedAt, value);
    }

    /// <summary>
    /// Gets the URL of the editor for a note.
    /// </summary>
    /// <param name="noteId">The note id.</param>
    /// <returns>The relative URL.</returns>
    public static string GetEditUrl(string noteId) => $"{FilesUrl}/edit/{Uri.EscapeDataString(noteId)}";

    /// <summary>
    /// Loads a note, unless it is already loaded.
    /// </summary>
    /// <param name="noteId">The note id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task LoadAsync(string noteId)
    {
        if (noteId == loadedNoteId)
        {
            return;
        }

        loadedNoteId = noteId;
        IsLoading = true;
        IsNotFound = false;
        ErrorMessage = null;
        LastSavedAt = null;
        try
        {
            var document = await noteService.GetNoteAsync(noteId);
            if (document == null)
            {
                Note = null;
                IsNotFound = true;
                return;
            }

            savedContent = document.Content;
            Note = document.Entry;
            FolderPath = document.FolderPath;
            Content = document.Content;
            PreviewHtml = markdownRenderer.ToHtml(Content);
            OnPropertyChanged(nameof(IsDirty));
        }
        catch (NotesException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (JSException)
        {
            ErrorMessage = FilesViewModel.StorageErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the note.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveAsync()
    {
        await TrySaveAsync();
    }

    /// <summary>
    /// Applies a format to the selected text in the editor.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task FormatAsync(MarkdownFormat format)
    {
        var selection = await editorInteropService.GetSelectionAsync(EditorElementId);
        if (selection == null)
        {
            return;
        }

        var edit = MarkdownFormatHelpers.Apply(selection.Text, selection.Start, selection.End, format);
        await editorInteropService.ApplyEditAsync(EditorElementId, edit);

        // The editor raises an input event which updates Content too; setting it here keeps the view model in step straight away.
        Content = edit.ApplyTo(selection.Text);
    }

    /// <summary>
    /// Shows or hides the preview pane.
    /// </summary>
    [RelayCommand]
    public void TogglePreview()
    {
        IsPreviewCollapsed = !IsPreviewCollapsed;
    }

    /// <summary>
    /// Returns to the files page (asking first if there are unsaved changes).
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        navigationService.NavigateTo(FilesUrl);
    }

    /// <summary>
    /// Decides whether the user can navigate away. With unsaved changes, the navigation is held and a dialog asks
    /// whether to save, discard or stay.
    /// </summary>
    /// <param name="targetLocation">Where the user is navigating to.</param>
    /// <returns>True if the navigation can go ahead.</returns>
    public bool CanLeave(string targetLocation)
    {
        if (!IsDirty || leaveConfirmed)
        {
            return true;
        }

        pendingLocation = targetLocation;
        IsLeaveDialogOpen = true;
        return false;
    }

    /// <summary>
    /// Closes the unsaved changes dialog and stays on the page.
    /// </summary>
    [RelayCommand]
    public void StayOnPage()
    {
        IsLeaveDialogOpen = false;
        pendingLocation = null;
    }

    /// <summary>
    /// Discards unsaved changes and carries on with the held navigation.
    /// </summary>
    [RelayCommand]
    public void DiscardAndLeave()
    {
        Leave();
    }

    /// <summary>
    /// Saves, then carries on with the held navigation. If saving fails the user stays on the page.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveAndLeaveAsync()
    {
        if (await TrySaveAsync())
        {
            Leave();
        }
        else
        {
            IsLeaveDialogOpen = false;
        }
    }

    /// <summary>
    /// Hides the error message.
    /// </summary>
    [RelayCommand]
    public void DismissError()
    {
        ErrorMessage = null;
    }

    private void Leave()
    {
        leaveConfirmed = true;
        IsLeaveDialogOpen = false;
        var location = pendingLocation ?? FilesUrl;
        pendingLocation = null;
        navigationService.NavigateTo(location);
    }

    private async Task<bool> TrySaveAsync()
    {
        if (Note == null || IsSaving)
        {
            return false;
        }

        IsSaving = true;
        ErrorMessage = null;
        var contentToSave = Content;
        try
        {
            Note = await noteService.SaveNoteAsync(Note.Id, contentToSave);
            savedContent = contentToSave;
            LastSavedAt = Note.ModifiedAt;
            OnPropertyChanged(nameof(IsDirty));
            return true;
        }
        catch (NotesException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        catch (JSException)
        {
            ErrorMessage = FilesViewModel.StorageErrorMessage;
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
