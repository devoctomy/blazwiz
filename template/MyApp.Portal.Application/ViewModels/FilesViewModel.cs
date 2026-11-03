using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.JSInterop;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the files page: a tree of the user's notes and folders, with dialogs to create, rename, move and
/// delete them, and drag and drop to move them between folders. The tree refreshes when a sync changes the notes.
/// </summary>
public partial class FilesViewModel : ViewModelBase
{
    /// <summary>
    /// The drop target id used for the top level.
    /// </summary>
    public const string TopLevelId = "";

    /// <summary>
    /// The name shown for the top level.
    /// </summary>
    public const string TopLevelName = "All notes";

    /// <summary>
    /// The message shown when the browser's storage cannot be used, e.g. in some private browsing modes.
    /// </summary>
    public const string StorageErrorMessage = "Your notes could not be read or saved in this browser's storage.";

    private readonly INoteService noteService;
    private readonly INavigationService navigationService;
    private readonly INoteNameService noteNameService;
    private readonly INoteTreeService noteTreeService;
    private readonly ISyncService syncService;
    private readonly ISyncEncryptionService encryptionService;
    private readonly HashSet<string> expandedFolderIds = [];
    private readonly IErrorText errorText;

    private IReadOnlyList<NoteEntry> entries = [];
    private IReadOnlyList<FileTreeItem> visibleItems = [];
    private IReadOnlyList<FolderOption> moveTargets = [];
    private bool isLoading = true;
    private bool isBusy;
    private string? errorMessage;
    private string? selectedFolderId;
    private FilesDialogKind activeDialog;
    private NoteEntry? dialogEntry;
    private string dialogName = string.Empty;
    private string? dialogError;
    private string moveTargetId = TopLevelId;
    private string? draggedId;
    private string? dropTargetId;
    private DateTimeOffset? lastSyncedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesViewModel"/> class.
    /// </summary>
    /// <param name="noteService">The note service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="noteNameService">The note name service.</param>
    /// <param name="noteTreeService">The note tree service.</param>
    /// <param name="syncService">The sync service, to refresh when a sync changes the notes.</param>
    /// <param name="encryptionService">Says whether encrypted notes can be decrypted on this device.</param>
    /// <param name="errorText">Describes the framework's errors (storage, encryption) for the user.</param>
    public FilesViewModel(
        INoteService noteService,
        INavigationService navigationService,
        INoteNameService noteNameService,
        INoteTreeService noteTreeService,
        ISyncService syncService,
        ISyncEncryptionService encryptionService,
        IErrorText errorText)
    {
        this.noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.noteNameService = noteNameService ?? throw new ArgumentNullException(nameof(noteNameService));
        this.noteTreeService = noteTreeService ?? throw new ArgumentNullException(nameof(noteTreeService));
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        lastSyncedAt = syncService.Status.LastSyncedAt;
        this.syncService.FilesChanged += OnFilesChanged;
        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.encryptionService.StatusChanged += OnEncryptionStatusChanged;
    }

    /// <summary>
    /// Gets the file extension given to every note, shown next to the name box.
    /// </summary>
    public string NoteExtension => noteNameService.NoteExtension;

    /// <summary>
    /// Gets the longest name allowed.
    /// </summary>
    public int MaxNameLength => noteNameService.MaxNameLength;

    /// <summary>
    /// Gets all of the user's notes and folders.
    /// </summary>
    public IReadOnlyList<NoteEntry> Entries
    {
        get => entries;
        private set => SetProperty(ref entries, value);
    }

    /// <summary>
    /// Gets the rows of the tree: top level entries plus the contents of expanded folders.
    /// </summary>
    public IReadOnlyList<FileTreeItem> VisibleItems
    {
        get => visibleItems;
        private set => SetProperty(ref visibleItems, value);
    }

    /// <summary>
    /// Gets a value indicating whether the entries are being loaded for the first time.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>
    /// Gets a value indicating whether a change is being saved.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Gets the last error, shown above the tree.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Gets the id of the selected folder, where new notes and folders are created. Null for the top level.
    /// </summary>
    public string? SelectedFolderId
    {
        get => selectedFolderId;
        private set
        {
            if (SetProperty(ref selectedFolderId, value))
            {
                OnPropertyChanged(nameof(SelectedFolderName));
            }
        }
    }

    /// <summary>
    /// Gets the name of the selected folder.
    /// </summary>
    public string SelectedFolderName => Entries.FirstOrDefault(e => e.Id == SelectedFolderId)?.Name ?? TopLevelName;

    /// <summary>
    /// Gets the dialog being shown.
    /// </summary>
    public FilesDialogKind ActiveDialog
    {
        get => activeDialog;
        private set => SetProperty(ref activeDialog, value);
    }

    /// <summary>
    /// Gets the note or folder the dialog is for (rename, move and delete).
    /// </summary>
    public NoteEntry? DialogEntry
    {
        get => dialogEntry;
        private set
        {
            if (SetProperty(ref dialogEntry, value))
            {
                OnPropertyChanged(nameof(DialogEntryDescendantCount));
            }
        }
    }

    /// <summary>
    /// Gets how many notes and folders are inside the folder the dialog is for, to warn before deleting it.
    /// </summary>
    public int DialogEntryDescendantCount => DialogEntry is { IsFolder: true } folder
        ? noteTreeService.GetDescendantIds(Entries, folder.Id).Count
        : 0;

    /// <summary>
    /// Gets or sets the name typed into the new note, new folder or rename dialog.
    /// </summary>
    public string DialogName
    {
        get => dialogName;
        set => SetProperty(ref dialogName, value);
    }

    /// <summary>
    /// Gets the validation error shown in the dialog.
    /// </summary>
    public string? DialogError
    {
        get => dialogError;
        private set => SetProperty(ref dialogError, value);
    }

    /// <summary>
    /// Gets the folders the dialog entry can be moved into.
    /// </summary>
    public IReadOnlyList<FolderOption> MoveTargets
    {
        get => moveTargets;
        private set => SetProperty(ref moveTargets, value);
    }

    /// <summary>
    /// Gets or sets the folder chosen in the move dialog (<see cref="TopLevelId"/> for the top level).
    /// </summary>
    public string MoveTargetId
    {
        get => moveTargetId;
        set => SetProperty(ref moveTargetId, value);
    }

    /// <summary>
    /// Gets the id of the entry being dragged.
    /// </summary>
    public string? DraggedId
    {
        get => draggedId;
        private set => SetProperty(ref draggedId, value);
    }

    /// <summary>
    /// Gets the id of the folder being dragged over (<see cref="TopLevelId"/> for the top level).
    /// </summary>
    public string? DropTargetId
    {
        get => dropTargetId;
        private set => SetProperty(ref dropTargetId, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await RunAsync(ReloadAsync);
        IsLoading = false;
    }

    /// <summary>
    /// Expands or collapses a folder.
    /// </summary>
    /// <param name="folderId">The folder id.</param>
    [RelayCommand]
    public void ToggleFolder(string folderId)
    {
        if (!expandedFolderIds.Remove(folderId))
        {
            expandedFolderIds.Add(folderId);
        }

        RefreshTree();
    }

    /// <summary>
    /// Selects a folder as the place for new notes and folders, and expands or collapses it.
    /// Selecting the selected folder again collapses it and selects the top level.
    /// </summary>
    /// <param name="folderId">The folder id.</param>
    [RelayCommand]
    public void SelectFolder(string folderId)
    {
        if (SelectedFolderId == folderId && expandedFolderIds.Contains(folderId))
        {
            expandedFolderIds.Remove(folderId);
            SelectedFolderId = null;
        }
        else
        {
            expandedFolderIds.Add(folderId);
            SelectedFolderId = folderId;
        }

        RefreshTree();
    }

    /// <summary>
    /// Selects the top level as the place for new notes and folders.
    /// </summary>
    [RelayCommand]
    public void SelectTopLevel()
    {
        SelectedFolderId = null;
    }

    /// <summary>
    /// Opens a note in the editor.
    /// </summary>
    /// <param name="noteId">The note id.</param>
    [RelayCommand]
    public void OpenNote(string noteId)
    {
        navigationService.NavigateTo(NoteEditorViewModel.GetEditUrl(noteId));
    }

    /// <summary>
    /// Shows the dialog asking for a new note's name.
    /// </summary>
    [RelayCommand]
    public void ShowNewNoteDialog()
    {
        ShowDialog(FilesDialogKind.NewNote, null, string.Empty);
    }

    /// <summary>
    /// Shows the dialog asking for a new folder's name.
    /// </summary>
    [RelayCommand]
    public void ShowNewFolderDialog()
    {
        ShowDialog(FilesDialogKind.NewFolder, null, string.Empty);
    }

    /// <summary>
    /// Shows the dialog asking for a new name for a note or folder.
    /// </summary>
    /// <param name="entryId">The entry id.</param>
    [RelayCommand]
    public void ShowRenameDialog(string entryId)
    {
        var entry = Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry != null)
        {
            ShowDialog(FilesDialogKind.Rename, entry, entry.IsFolder ? entry.Name : noteNameService.WithoutExtension(entry.Name));
        }
    }

    /// <summary>
    /// Shows the dialog asking which folder to move a note or folder into.
    /// </summary>
    /// <param name="entryId">The entry id.</param>
    [RelayCommand]
    public void ShowMoveDialog(string entryId)
    {
        var entry = Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry != null)
        {
            MoveTargets = noteTreeService.BuildMoveTargets(Entries, entry.Id, TopLevelName);
            MoveTargetId = entry.ParentId ?? TopLevelId;
            ShowDialog(FilesDialogKind.Move, entry, string.Empty);
        }
    }

    /// <summary>
    /// Shows the dialog confirming a note or folder should be deleted.
    /// </summary>
    /// <param name="entryId">The entry id.</param>
    [RelayCommand]
    public void ShowDeleteDialog(string entryId)
    {
        var entry = Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry != null)
        {
            ShowDialog(FilesDialogKind.Delete, entry, string.Empty);
        }
    }

    /// <summary>
    /// Closes the dialog without making any changes.
    /// </summary>
    [RelayCommand]
    public void CloseDialog()
    {
        ActiveDialog = FilesDialogKind.None;
        DialogEntry = null;
        DialogError = null;
    }

    /// <summary>
    /// Carries out the action of the open dialog. Validation errors are shown in the dialog, which stays open.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ConfirmDialogAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        DialogError = null;
        try
        {
            string? openNoteId = null;
            switch (ActiveDialog)
            {
                case FilesDialogKind.NewNote:
                    openNoteId = (await noteService.CreateNoteAsync(DialogName, SelectedFolderId)).Id;
                    break;
                case FilesDialogKind.NewFolder:
                    var folder = await noteService.CreateFolderAsync(DialogName, SelectedFolderId);
                    if (SelectedFolderId != null)
                    {
                        expandedFolderIds.Add(SelectedFolderId);
                    }

                    SelectedFolderId = folder.Id;
                    break;
                case FilesDialogKind.Rename when DialogEntry != null:
                    await noteService.RenameAsync(DialogEntry.Id, DialogName);
                    break;
                case FilesDialogKind.Move when DialogEntry != null:
                    await MoveEntryAsync(DialogEntry.Id, MoveTargetId);
                    break;
                case FilesDialogKind.Delete when DialogEntry != null:
                    await DeleteEntryAsync(DialogEntry);
                    break;
            }

            CloseDialog();
            if (openNoteId != null)
            {
                OpenNote(openNoteId);
                return;
            }

            await ReloadAsync();
        }
        catch (Exception ex) when (ex is NotesException or SyncStorageAccessException or SyncStorageMigrationException or E2eLockedException)
        {
            DialogError = Describe(ex);
        }
        catch (JSException)
        {
            DialogError = StorageErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Records that a note or folder has started being dragged.
    /// </summary>
    /// <param name="entryId">The entry id.</param>
    [RelayCommand]
    public void StartDrag(string entryId)
    {
        DraggedId = entryId;
    }

    /// <summary>
    /// Records that dragging has finished, whether or not the entry was dropped.
    /// </summary>
    [RelayCommand]
    public void EndDrag()
    {
        DraggedId = null;
        DropTargetId = null;
    }

    /// <summary>
    /// Highlights a folder (or the top level) as the drop target, if the dragged entry can be moved there.
    /// </summary>
    /// <param name="targetId">The folder id, or <see cref="TopLevelId"/>.</param>
    [RelayCommand]
    public void DragEnter(string targetId)
    {
        DropTargetId = CanDropOn(targetId) ? targetId : null;
    }

    /// <summary>
    /// Moves the dragged entry into a folder (or the top level).
    /// </summary>
    /// <param name="targetId">The folder id, or <see cref="TopLevelId"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task DropAsync(string targetId)
    {
        var entryId = DraggedId;
        EndDrag();
        if (entryId == null || !CanDropOn(targetId, entryId))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await MoveEntryAsync(entryId, targetId);
            await ReloadAsync();
        });
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
    /// Gets whether a note is end to end encrypted, and if so whether this device can decrypt it, for its lock.
    /// </summary>
    /// <param name="entry">The note.</param>
    /// <returns>Null when not encrypted; true when it can be decrypted here (green lock); false when not (red lock).</returns>
    public bool? CanDecrypt(NoteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.EncryptionKeyId == null ? null : encryptionService.CanDecrypt(entry.EncryptionKeyId);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            syncService.FilesChanged -= OnFilesChanged;
            syncService.StatusChanged -= OnSyncStatusChanged;
            encryptionService.StatusChanged -= OnEncryptionStatusChanged;
        }

        base.Dispose(disposing);
    }

    private bool CanDropOn(string targetId, string? entryId = null)
    {
        entryId ??= DraggedId;
        var entry = Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry == null || (entry.ParentId ?? TopLevelId) == targetId)
        {
            return false;
        }

        if (targetId == TopLevelId)
        {
            return true;
        }

        var target = Entries.FirstOrDefault(e => e.Id == targetId);
        return target is { IsFolder: true }
            && target.Id != entry.Id
            && !(entry.IsFolder && noteTreeService.GetDescendantIds(Entries, entry.Id).Contains(target.Id));
    }

    private async Task MoveEntryAsync(string entryId, string targetId)
    {
        var parentId = targetId == TopLevelId ? null : targetId;
        await noteService.MoveAsync(entryId, parentId);
        if (parentId != null)
        {
            expandedFolderIds.Add(parentId);
        }
    }

    private async Task DeleteEntryAsync(NoteEntry entry)
    {
        var removedIds = entry.IsFolder ? noteTreeService.GetDescendantIds(Entries, entry.Id).Append(entry.Id).ToHashSet() : [entry.Id];
        await noteService.DeleteAsync(entry.Id);
        if (SelectedFolderId != null && removedIds.Contains(SelectedFolderId))
        {
            SelectedFolderId = null;
        }
    }

    private void ShowDialog(FilesDialogKind kind, NoteEntry? entry, string name)
    {
        DialogEntry = entry;
        DialogName = name;
        DialogError = null;
        ActiveDialog = kind;
    }

    private async Task ReloadAsync()
    {
        Entries = await noteService.GetEntriesAsync();
        expandedFolderIds.RemoveWhere(id => !Entries.Any(e => e.Id == id));
        if (SelectedFolderId != null && !Entries.Any(e => e.Id == SelectedFolderId))
        {
            SelectedFolderId = null;
        }

        OnPropertyChanged(nameof(SelectedFolderName));
        RefreshTree();
    }

    private void RefreshTree()
    {
        VisibleItems = noteTreeService.BuildVisibleItems(Entries, expandedFolderIds);
    }

    private async void OnFilesChanged(object? sender, SyncFilesChangedEventArgs e)
    {
        if (e.Profile == NoteService.ProfileName && !IsBusy)
        {
            await ReloadSafelyAsync();
        }
    }

    private async void OnSyncStatusChanged(object? sender, SyncStatus status)
    {
        // A finished sync may make it time to create the welcome note, or show notes from another device.
        if (status.LastSyncedAt != lastSyncedAt)
        {
            lastSyncedAt = status.LastSyncedAt;
            if (!IsBusy)
            {
                await ReloadSafelyAsync();
            }
        }
    }

    private async void OnEncryptionStatusChanged(object? sender, E2eStatus status)
    {
        // The locks change colour, and notes may now be readable or newly encrypted.
        if (!IsBusy)
        {
            await ReloadSafelyAsync();
        }
    }

    private async Task ReloadSafelyAsync()
    {
        try
        {
            await RunAsync(ReloadAsync);
        }
        catch (Exception)
        {
            // A background refresh must not crash the page; the next change or visit loads the notes again.
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is NotesException or SyncStorageAccessException or SyncStorageMigrationException or E2eLockedException)
        {
            ErrorMessage = Describe(ex);
        }
        catch (JSException)
        {
            ErrorMessage = StorageErrorMessage;
        }
    }

    private string Describe(Exception ex) => ex is NotesException ? ex.Message : errorText.Describe(ex);
}
