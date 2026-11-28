using System.ComponentModel;
using System.Text;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.JSInterop;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.Sync;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the note editor: markdown text with formatting buttons, a live preview and saving. When a sync changes
/// the open note, it is reloaded, or (with unsaved edits) the user is asked whether to keep their version or take the
/// synced one. The note's past versions are in <see cref="History"/>: one picked there is shown read only, in place of
/// the note, until the user goes back to the current version or restores it.
/// </summary>
public partial class NoteEditorViewModel : ViewModelBase, IFileHistoryHost
{
    /// <summary>
    /// The id of the editor's textarea.
    /// </summary>
    public const string EditorElementId = "note-editor";

    /// <summary>
    /// The URL of the files page.
    /// </summary>
    public const string FilesUrl = "files";

    /// <summary>
    /// The query string parameter holding the note's key in the editor URL.
    /// </summary>
    public const string NoteQueryParameter = "note";

    /// <summary>
    /// The query parameter naming the note's notebook (its sync profile); without it, the default notebook.
    /// </summary>
    public const string NotebookQueryParameter = "notebook";

    /// <summary>
    /// The message shown when the open note was deleted on another device.
    /// </summary>
    public const string RemoteDeletedMessage = "This note was deleted on another device.";

    private readonly INotebookService notebookService;
    private readonly INoteServiceFactory noteServiceFactory;
    private readonly ISyncProtectedProfileService profiles;
    private readonly IMarkdownRenderer markdownRenderer;
    private readonly IMarkdownFormatService markdownFormatService;
    private readonly INoteEditorInteropService editorInteropService;
    private readonly INavigationService navigationService;
    private readonly ISyncService syncService;
    private readonly IE2eProtectedProfileManager manager;
    private readonly IErrorText errorText;

    private string? loadedNoteId;
    private string? loadedNotebook;
    private Notebook? notebook;
    private INoteService? noteService;
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
    private bool isRemoteChangeDialogOpen;
    private bool isRemoteDeleted;
    private string versionContent = string.Empty;
    private string versionPreviewHtml = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteEditorViewModel"/> class.
    /// </summary>
    /// <param name="notebookService">The user's notebooks.</param>
    /// <param name="noteServiceFactory">Creates the note service of the note's notebook.</param>
    /// <param name="profiles">Says when the user's notebooks are loaded.</param>
    /// <param name="markdownRenderer">The markdown renderer, for the preview.</param>
    /// <param name="markdownFormatService">Works out the edits made by the formatting buttons.</param>
    /// <param name="editorInteropService">Access to the editor's text and selection in the browser.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="syncService">The sync service, to notice when a sync changes the open note.</param>
    /// <param name="manager">Says whether the note's notebook is unlocked, and when that changes.</param>
    /// <param name="errorText">Describes the framework's errors (storage, encryption) for the user.</param>
    /// <param name="history">The note's past versions.</param>
    public NoteEditorViewModel(
        INotebookService notebookService,
        INoteServiceFactory noteServiceFactory,
        ISyncProtectedProfileService profiles,
        IMarkdownRenderer markdownRenderer,
        IMarkdownFormatService markdownFormatService,
        INoteEditorInteropService editorInteropService,
        INavigationService navigationService,
        ISyncService syncService,
        IE2eProtectedProfileManager manager,
        IErrorText errorText,
        FileHistoryViewModel history)
    {
        // #lizard forgives: only checks and stores the injected services.
        this.notebookService = notebookService ?? throw new ArgumentNullException(nameof(notebookService));
        this.noteServiceFactory = noteServiceFactory ?? throw new ArgumentNullException(nameof(noteServiceFactory));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.markdownRenderer = markdownRenderer ?? throw new ArgumentNullException(nameof(markdownRenderer));
        this.markdownFormatService = markdownFormatService ?? throw new ArgumentNullException(nameof(markdownFormatService));
        this.editorInteropService = editorInteropService ?? throw new ArgumentNullException(nameof(editorInteropService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        History = history ?? throw new ArgumentNullException(nameof(history));
        this.syncService.FilesChanged += OnFilesChanged;
        this.manager.LockStateChanged += OnLockStateChanged;
        this.profiles.ProfilesChanged += OnProfilesChanged;
        History.PropertyChanged += OnHistoryChanged;
        History.ShownChanged += OnShownVersionChanged;
    }

    /// <summary>
    /// Gets the note's past versions: the History dropdown, and the bar above a past version shown.
    /// </summary>
    public FileHistoryViewModel History { get; }

    /// <summary>
    /// Gets a value indicating whether a past version of the note is shown, read only, in place of the note.
    /// </summary>
    public bool IsViewingVersion => History.IsViewingVersion;

    /// <summary>
    /// Gets the markdown of the past version shown.
    /// </summary>
    public string VersionContent
    {
        get => versionContent;
        private set
        {
            if (SetProperty(ref versionContent, value))
            {
                OnPropertyChanged(nameof(EditorText));
            }
        }
    }

    /// <summary>
    /// Gets or sets what the editor shows: the note being edited, or, read only, the past version shown. Edits are
    /// ignored while a past version is shown.
    /// </summary>
    public string EditorText
    {
        get => IsViewingVersion ? VersionContent : Content;
        set
        {
            if (!IsViewingVersion)
            {
                Content = value;
            }
        }
    }

    /// <summary>
    /// Gets the preview of the past version shown, or of the note, as HTML.
    /// </summary>
    public string ShownPreviewHtml => IsViewingVersion ? versionPreviewHtml : PreviewHtml;

    /// <inheritdoc/>
    public bool HasUnsavedChanges => IsDirty;

    /// <summary>
    /// Gets the note's notebook, or null until it is loaded.
    /// </summary>
    public Notebook? Notebook
    {
        get => notebook;
        private set => SetProperty(ref notebook, value);
    }

    /// <summary>
    /// Gets a value indicating whether the note's notebook is unlocked. While it is locked, the page shows the unlock
    /// panel in place of the editor, and keeps any unsaved edits.
    /// </summary>
    public bool IsUnlocked => Notebook is { } open && manager.IsUnlocked(new E2eKeyScope(open.Id));

    /// <summary>
    /// Gets the URL of the files page of the note's notebook.
    /// </summary>
    public string FilesPageUrl => GetFilesUrl(Notebook?.Name);

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
                OnPropertyChanged(nameof(EditorText));
            }
        }
    }

    /// <summary>
    /// Gets the preview of <see cref="Content"/> as HTML. Raw HTML in the markdown is escaped, so it is safe to display.
    /// </summary>
    public string PreviewHtml
    {
        get => previewHtml;
        private set
        {
            if (SetProperty(ref previewHtml, value))
            {
                OnPropertyChanged(nameof(ShownPreviewHtml));
            }
        }
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
    /// Gets a value indicating whether the dialog asking what to do about a note changed by a sync is showing.
    /// </summary>
    public bool IsRemoteChangeDialogOpen
    {
        get => isRemoteChangeDialogOpen;
        private set => SetProperty(ref isRemoteChangeDialogOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sync deleted the note (rather than changed it).
    /// </summary>
    public bool IsRemoteDeleted
    {
        get => isRemoteDeleted;
        private set => SetProperty(ref isRemoteDeleted, value);
    }

    private INoteService Notes => noteService ?? throw new InvalidOperationException("No notebook is open.");

    /// <summary>
    /// Gets the URL of the files page of a notebook.
    /// </summary>
    /// <param name="notebookName">The notebook's sync profile name, or null for the default notebook.</param>
    /// <returns>The relative URL.</returns>
    public static string GetFilesUrl(string? notebookName)
    {
        return notebookName == null || notebookName == NoteService.ProfileName
            ? FilesUrl
            : $"{FilesUrl}?{NotebookQueryParameter}={Uri.EscapeDataString(notebookName)}";
    }

    /// <summary>
    /// Gets the URL of the editor for a note.
    /// </summary>
    /// <param name="notebookName">The note's notebook (its sync profile name), or null for the default notebook.</param>
    /// <param name="noteId">The note id (its key, e.g. <c>notes/Work/Ideas.md</c>).</param>
    /// <returns>The relative URL.</returns>
    public static string GetEditUrl(string? notebookName, string noteId)
    {
        ArgumentNullException.ThrowIfNull(noteId);

        var notebookQuery = notebookName == null || notebookName == NoteService.ProfileName
            ? string.Empty
            : $"{NotebookQueryParameter}={Uri.EscapeDataString(notebookName)}&";
        return $"{FilesUrl}/edit?{notebookQuery}{NoteQueryParameter}={Uri.EscapeDataString(noteId)}";
    }

    /// <summary>
    /// Loads a note, unless it is already loaded.
    /// </summary>
    /// <param name="notebookName">The note's notebook (its sync profile name), or null for the default notebook.</param>
    /// <param name="noteId">The note id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task LoadAsync(string? notebookName, string noteId)
    {
        if (noteId == loadedNoteId && notebookName == loadedNotebook)
        {
            return;
        }

        loadedNoteId = noteId;
        loadedNotebook = notebookName;
        IsLoading = true;
        IsNotFound = false;
        ErrorMessage = null;
        LastSavedAt = null;
        try
        {
            Notebook = await notebookService.FindAsync(notebookName);
            noteService = Notebook == null ? null : noteServiceFactory.Create(Notebook);
            OnPropertyChanged(nameof(IsUnlocked));
            OnPropertyChanged(nameof(FilesPageUrl));
            var document = noteService == null ? null : await noteService.GetNoteAsync(noteId);
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
            History.SetFile(Notebook!.Name, noteId, this);
        }
        catch (Exception ex) when (ex is NotesException or SyncStorageAccessException or SyncStorageMigrationException or E2eLockedException)
        {
            ErrorMessage = Describe(ex);
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

    /// <inheritdoc/>
    public Task<bool> SaveChangesAsync() => TrySaveAsync();

    /// <inheritdoc/>
    public void DiscardChanges()
    {
        Content = savedContent;
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
        if (IsViewingVersion)
        {
            // A past version is read only.
            return;
        }

        var selection = await editorInteropService.GetSelectionAsync(EditorElementId);
        if (selection == null)
        {
            return;
        }

        var edit = markdownFormatService.Apply(selection.Text, selection.Start, selection.End, format);
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
        navigationService.NavigateTo(FilesPageUrl);
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

    /// <summary>
    /// Keeps the user's unsaved edits after a sync changed or deleted the note. Saving uploads them over the synced version.
    /// </summary>
    [RelayCommand]
    public void KeepMine()
    {
        IsRemoteChangeDialogOpen = false;
    }

    /// <summary>
    /// Discards the user's unsaved edits and takes the synced version (or, if it was deleted, returns to the files page).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task TakeTheirsAsync()
    {
        IsRemoteChangeDialogOpen = false;
        if (IsRemoteDeleted)
        {
            leaveConfirmed = true;
            navigationService.NavigateTo(FilesPageUrl);
            return;
        }

        await ReloadAsync();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            syncService.FilesChanged -= OnFilesChanged;
            manager.LockStateChanged -= OnLockStateChanged;
            profiles.ProfilesChanged -= OnProfilesChanged;
            History.PropertyChanged -= OnHistoryChanged;
            History.ShownChanged -= OnShownVersionChanged;
        }

        base.Dispose(disposing);
    }

    // The page shows the history's dropdown and bar, so it is shown again whenever they change.
    private void OnHistoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(History));
        if (e.PropertyName == nameof(FileHistoryViewModel.IsViewingVersion))
        {
            OnPropertyChanged(nameof(IsViewingVersion));
            OnPropertyChanged(nameof(ShownPreviewHtml));
            OnPropertyChanged(nameof(EditorText));
        }
    }

    private async void OnShownVersionChanged(object? sender, EventArgs e)
    {
        if (History.SelectedContent is { } shown)
        {
            VersionContent = Encoding.UTF8.GetString(shown.Content);
            versionPreviewHtml = markdownRenderer.ToHtml(VersionContent);
            OnPropertyChanged(nameof(ShownPreviewHtml));
            return;
        }

        // Back to the current version, which may have been restored: read it again.
        VersionContent = string.Empty;
        versionPreviewHtml = string.Empty;
        try
        {
            await ReloadAsync();
        }
        catch (Exception)
        {
            // A background reload must not crash the page; opening the note again reads it.
        }
    }

    private async void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (Notebook == null || scope.Id != Notebook.Id)
        {
            return;
        }

        OnPropertyChanged(nameof(IsUnlocked));
        if (IsUnlocked && Note == null && loadedNoteId is { } noteId)
        {
            // It could not be read while locked: load it now.
            loadedNoteId = null;
            try
            {
                await LoadAsync(loadedNotebook, noteId);
            }
            catch (Exception)
            {
                // A background load must not crash the page; opening the note again loads it.
            }
        }
    }

    private async void OnProfilesChanged(object? sender, EventArgs e)
    {
        if (Notebook == null && loadedNoteId is { } noteId)
        {
            // The notebooks were not loaded yet when the page opened.
            loadedNoteId = null;
            try
            {
                await LoadAsync(loadedNotebook, noteId);
            }
            catch (Exception)
            {
                // A background load must not crash the page; opening the note again loads it.
            }
        }
    }

    private async void OnFilesChanged(object? sender, SyncFilesChangedEventArgs e)
    {
        var open = Note;
        if (e.Profile != Notebook?.Name || open == null || !e.Keys.Contains(open.Id))
        {
            return;
        }

        try
        {
            var document = await Notes.GetNoteAsync(open.Id);
            if (IsDirty)
            {
                IsRemoteDeleted = document == null;
                IsRemoteChangeDialogOpen = true;
            }
            else
            {
                ApplyDocument(document);
            }
        }
        catch (Exception)
        {
            // Leave the editor as it is; the user can still save their version.
        }
    }

    private async Task ReloadAsync()
    {
        if (Note == null)
        {
            return;
        }

        try
        {
            ApplyDocument(await Notes.GetNoteAsync(Note.Id));
        }
        catch (Exception ex) when (ex is NotesException or SyncStorageAccessException or SyncStorageMigrationException or E2eLockedException)
        {
            ErrorMessage = Describe(ex);
        }
        catch (JSException)
        {
            ErrorMessage = FilesViewModel.StorageErrorMessage;
        }
    }

    private void ApplyDocument(NoteDocument? document)
    {
        if (document == null)
        {
            Note = null;
            IsNotFound = true;
            ErrorMessage = RemoteDeletedMessage;
            return;
        }

        savedContent = document.Content;
        Note = document.Entry;
        FolderPath = document.FolderPath;
        Content = document.Content;
        PreviewHtml = markdownRenderer.ToHtml(Content);
        OnPropertyChanged(nameof(IsDirty));
    }

    private void Leave()
    {
        leaveConfirmed = true;
        IsLeaveDialogOpen = false;
        var location = pendingLocation ?? FilesPageUrl;
        pendingLocation = null;
        navigationService.NavigateTo(location);
    }

    private async Task<bool> TrySaveAsync()
    {
        if (Note == null || IsSaving || IsViewingVersion)
        {
            // Nothing to save while a past version is shown: the editor is read only.
            return false;
        }

        IsSaving = true;
        ErrorMessage = null;
        var contentToSave = Content;
        try
        {
            Note = await Notes.SaveNoteAsync(Note.Id, contentToSave);
            savedContent = contentToSave;
            LastSavedAt = Note.ModifiedAt;
            OnPropertyChanged(nameof(IsDirty));
            return true;
        }
        catch (Exception ex) when (ex is NotesException or SyncStorageAccessException or SyncStorageMigrationException or E2eLockedException)
        {
            ErrorMessage = Describe(ex);
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

    private string Describe(Exception ex) => ex is NotesException ? ex.Message : errorText.Describe(ex);
}
