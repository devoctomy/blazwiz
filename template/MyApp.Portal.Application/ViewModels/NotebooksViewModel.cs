using System.ComponentModel;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the notebooks page: the user's notebooks, each with its own passphrase, and a dialog to create one.
/// Opening a notebook shows its pages on the files page. The dialog's passphrase fields and Advanced tab are the
/// framework's passphrase form (<see cref="E2ePassphraseFormViewModel"/>); only the notebook's name is the app's.
/// Passphrases are cleared once used.
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
    private bool isWorking;
    private string? dialogError;
    private string? openingNotebook;
    private Notebook? forgetCandidate;
    private string? forgetError;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotebooksViewModel"/> class.
    /// </summary>
    /// <param name="notebookService">The user's notebooks.</param>
    /// <param name="profiles">Says when notebooks are loaded, created or deleted.</param>
    /// <param name="manager">Says when a notebook is unlocked or locked.</param>
    /// <param name="navigationService">Opens a notebook.</param>
    /// <param name="errorText">Describes the framework's errors (e.g. a passphrase too short).</param>
    /// <param name="passphraseForm">The framework's form for a new passphrase and its Argon2id settings.</param>
    public NotebooksViewModel(
        INotebookService notebookService,
        ISyncProtectedProfileService profiles,
        IE2eProtectedProfileManager manager,
        INavigationService navigationService,
        IErrorText errorText,
        E2ePassphraseFormViewModel passphraseForm)
    {
        PassphraseForm = passphraseForm ?? throw new ArgumentNullException(nameof(passphraseForm));
        this.notebookService = notebookService ?? throw new ArgumentNullException(nameof(notebookService));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.profiles.ProfilesChanged += OnProfilesChanged;
        this.manager.LockStateChanged += OnLockStateChanged;
        PassphraseForm.PropertyChanged += OnPassphraseFormChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the notebook may be created: false while it is being created, and while the
    /// Argon2id settings on the Advanced tab were changed and not benchmarked yet.
    /// </summary>
    public bool CanCreate => !IsWorking && PassphraseForm.CanConfirm;

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
    /// Gets the new notebook's passphrase form: the passphrase on the dialog's first tab, and on its Advanced tab the
    /// Argon2id settings of the passphrase's key.
    /// </summary>
    public E2ePassphraseFormViewModel PassphraseForm { get; }

    /// <summary>
    /// Gets a value indicating whether the notebook is being created.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set
        {
            if (SetProperty(ref isWorking, value))
            {
                OnPropertyChanged(nameof(CanCreate));
            }
        }
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
    /// Gets the name of the notebook being unlocked with biometrics to open it, or null.
    /// </summary>
    public string? OpeningNotebook
    {
        get => openingNotebook;
        private set => SetProperty(ref openingNotebook, value);
    }

    /// <summary>
    /// Gets the notebook the user is asked to confirm forgetting biometrics for, or null when the dialog is closed.
    /// </summary>
    public Notebook? ForgetCandidate
    {
        get => forgetCandidate;
        private set => SetProperty(ref forgetCandidate, value);
    }

    /// <summary>
    /// Gets why biometrics could not be forgotten, or null.
    /// </summary>
    public string? ForgetError
    {
        get => forgetError;
        private set => SetProperty(ref forgetError, value);
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
    /// Opens a notebook's pages. A locked notebook that biometrics can unlock on this device is unlocked with them first
    /// (synced first when online), so it opens straight onto its pages; if the passkey is cancelled or fails, its
    /// passphrase is asked for there as usual.
    /// </summary>
    /// <param name="name">The notebook's sync profile name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task OpenAsync(string name)
    {
        if (OpeningNotebook != null)
        {
            return;
        }

        if (Notebooks.FirstOrDefault(n => n.Name == name) is { CanUnlockWithBiometrics: true, IsUnlocked: false } notebook)
        {
            OpeningNotebook = name;
            try
            {
                await notebookService.UnlockWithBiometricsAsync(notebook);
            }
            finally
            {
                OpeningNotebook = null;
            }
        }

        navigationService.NavigateTo(NoteEditorViewModel.GetFilesUrl(name));
    }

    /// <summary>
    /// Asks the user to confirm they want to stop unlocking a notebook with biometrics on this device.
    /// </summary>
    /// <param name="notebook">The notebook.</param>
    [RelayCommand]
    public void AskForgetBiometrics(Notebook notebook)
    {
        ArgumentNullException.ThrowIfNull(notebook);

        ForgetError = null;
        ForgetCandidate = notebook;
    }

    /// <summary>
    /// Closes the forget biometrics dialog without forgetting them. Ignored while they are being forgotten.
    /// </summary>
    [RelayCommand]
    public void CancelForgetBiometrics()
    {
        if (IsWorking)
        {
            return;
        }

        ForgetCandidate = null;
        ForgetError = null;
    }

    /// <summary>
    /// Stops unlocking the notebook being confirmed with biometrics on this device. Its passphrase still unlocks it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ForgetBiometricsAsync()
    {
        if (IsWorking || ForgetCandidate is not { } notebook)
        {
            return;
        }

        ForgetError = null;
        IsWorking = true;
        try
        {
            await notebookService.ForgetBiometricsAsync(notebook);
            ForgetCandidate = null;
        }
        catch (Exception ex)
        {
            ForgetError = errorText.Describe(ex);
        }
        finally
        {
            IsWorking = false;
            await RefreshSafelyAsync();
        }
    }

    /// <summary>
    /// Opens the new notebook dialog with empty fields, and the Argon2id settings new notebooks are given.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ShowCreateDialogAsync()
    {
        await PassphraseForm.ResetAsync();
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

        PassphraseForm.ClearPassphrases();
        IsCreateDialogOpen = false;
    }

    /// <summary>
    /// Creates the notebook, once the dialog's fields are valid, and opens it. Argon2id settings that were changed must
    /// have been benchmarked first.
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
            var notebook = await notebookService.CreateAsync(Title, PassphraseForm.Passphrase, PassphraseForm.GetArgon2Parameters());
            PassphraseForm.ClearPassphrases();
            IsCreateDialogOpen = false;
            navigationService.NavigateTo(NoteEditorViewModel.GetFilesUrl(notebook.Name));
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
            PassphraseForm.PropertyChanged -= OnPassphraseFormChanged;
        }

        base.Dispose(disposing);
    }

    // The name is the app's; the passphrase and its Argon2id settings are checked by the framework's form.
    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            PassphraseForm.ShowTab(E2ePassphraseFormTab.Passphrase);
            return "Give the notebook a name.";
        }

        return PassphraseForm.Validate();
    }

    private async Task RefreshAsync()
    {
        Notebooks = await notebookService.GetNotebooksAsync();
    }

    // The dialog shows the form again whenever it changes, and the Create button follows the form: disabled while its
    // Argon2id settings need a benchmark.
    private void OnPassphraseFormChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PassphraseForm));
        if (e.PropertyName == nameof(E2ePassphraseFormViewModel.CanConfirm))
        {
            OnPropertyChanged(nameof(CanCreate));
        }
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
