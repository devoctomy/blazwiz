using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the notebooks page: the user's notebooks, each with its own passphrase, and a dialog to create one.
/// Opening a notebook shows its pages on the files page. Passphrases are cleared from the view model once used.
/// </summary>
public partial class NotebooksViewModel : ViewModelBase
{
    private readonly INotebookService notebookService;
    private readonly ISyncProtectedProfileService profiles;
    private readonly IE2eProtectedProfileManager manager;
    private readonly INavigationService navigationService;
    private readonly IErrorText errorText;

    private IReadOnlyList<Notebook> notebooks = [];
    private bool isLoading = true;
    private bool isCreateDialogOpen;
    private string title = string.Empty;
    private string passphrase = string.Empty;
    private string confirmPassphrase = string.Empty;
    private bool acknowledgedNoRecovery;
    private bool isWorking;
    private string? dialogError;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotebooksViewModel"/> class.
    /// </summary>
    /// <param name="notebookService">The user's notebooks.</param>
    /// <param name="profiles">Says when notebooks are loaded, created or deleted.</param>
    /// <param name="manager">Says when a notebook is unlocked or locked.</param>
    /// <param name="navigationService">Opens a notebook.</param>
    /// <param name="errorText">Describes the framework's errors (e.g. a passphrase too short).</param>
    public NotebooksViewModel(
        INotebookService notebookService,
        ISyncProtectedProfileService profiles,
        IE2eProtectedProfileManager manager,
        INavigationService navigationService,
        IErrorText errorText)
    {
        this.notebookService = notebookService ?? throw new ArgumentNullException(nameof(notebookService));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.profiles.ProfilesChanged += OnProfilesChanged;
        this.manager.LockStateChanged += OnLockStateChanged;
    }

    /// <summary>
    /// Gets the user's notebooks: the default notebook first.
    /// </summary>
    public IReadOnlyList<Notebook> Notebooks
    {
        get => notebooks;
        private set => SetProperty(ref notebooks, value);
    }

    /// <summary>
    /// Gets a value indicating whether the notebooks are being loaded for the first time.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>
    /// Gets a value indicating whether the new notebook dialog is open.
    /// </summary>
    public bool IsCreateDialogOpen
    {
        get => isCreateDialogOpen;
        private set => SetProperty(ref isCreateDialogOpen, value);
    }

    /// <summary>
    /// Gets or sets the new notebook's name.
    /// </summary>
    public string Title
    {
        get => title;
        set => SetProperty(ref title, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the new notebook's passphrase.
    /// </summary>
    public string Passphrase
    {
        get => passphrase;
        set => SetProperty(ref passphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the passphrase entered again, to confirm it.
    /// </summary>
    public string ConfirmPassphrase
    {
        get => confirmPassphrase;
        set => SetProperty(ref confirmPassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user has confirmed there is no passphrase recovery.
    /// </summary>
    public bool AcknowledgedNoRecovery
    {
        get => acknowledgedNoRecovery;
        set => SetProperty(ref acknowledgedNoRecovery, value);
    }

    /// <summary>
    /// Gets a value indicating whether the notebook is being created.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => SetProperty(ref isWorking, value);
    }

    /// <summary>
    /// Gets why the notebook was not created, or null.
    /// </summary>
    public string? DialogError
    {
        get => dialogError;
        private set => SetProperty(ref dialogError, value);
    }

    /// <summary>
    /// Describes where a notebook stands, as its row says it.
    /// </summary>
    /// <param name="notebook">The notebook.</param>
    /// <returns>The text.</returns>
    public static string GetStateText(Notebook notebook)
    {
        ArgumentNullException.ThrowIfNull(notebook);

        return !notebook.HasPassphrase ? "No passphrase yet" : notebook.IsUnlocked ? "Unlocked" : "Locked";
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        IsLoading = false;
    }

    /// <summary>
    /// Opens a notebook's pages.
    /// </summary>
    /// <param name="name">The notebook's sync profile name.</param>
    [RelayCommand]
    public void Open(string name)
    {
        navigationService.NavigateTo(NoteEditorViewModel.GetFilesUrl(name));
    }

    /// <summary>
    /// Opens the new notebook dialog with empty fields.
    /// </summary>
    [RelayCommand]
    public void ShowCreateDialog()
    {
        ClearInputs();
        Title = string.Empty;
        DialogError = null;
        IsCreateDialogOpen = true;
    }

    /// <summary>
    /// Closes the new notebook dialog. Ignored while the notebook is being created.
    /// </summary>
    [RelayCommand]
    public void CloseCreateDialog()
    {
        if (IsWorking)
        {
            return;
        }

        ClearInputs();
        IsCreateDialogOpen = false;
    }

    /// <summary>
    /// Creates the notebook, once the dialog's fields are valid, and opens it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task CreateAsync()
    {
        if (IsWorking)
        {
            return;
        }

        DialogError = Validate();
        if (DialogError != null)
        {
            return;
        }

        IsWorking = true;
        try
        {
            var notebook = await notebookService.CreateAsync(Title, Passphrase);
            ClearInputs();
            IsCreateDialogOpen = false;
            Open(notebook.Name);
        }
        catch (Exception ex)
        {
            DialogError = ex is NotesException ? ex.Message : errorText.Describe(ex);
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            profiles.ProfilesChanged -= OnProfilesChanged;
            manager.LockStateChanged -= OnLockStateChanged;
        }

        base.Dispose(disposing);
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return "Give the notebook a name.";
        }

        if (string.IsNullOrEmpty(Passphrase))
        {
            return "Enter a passphrase.";
        }

        if (!string.Equals(Passphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            return "The passphrases do not match.";
        }

        return AcknowledgedNoRecovery ? null : "Confirm you understand there is no passphrase recovery.";
    }

    private void ClearInputs()
    {
        Passphrase = string.Empty;
        ConfirmPassphrase = string.Empty;
        AcknowledgedNoRecovery = false;
    }

    private async Task RefreshAsync()
    {
        Notebooks = await notebookService.GetNotebooksAsync();
    }

    private async void OnProfilesChanged(object? sender, EventArgs e)
    {
        await RefreshSafelyAsync();
    }

    private async void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        await RefreshSafelyAsync();
    }

    private async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception)
        {
            // A background refresh must not crash the page; the next change refreshes it again.
        }
    }
}
