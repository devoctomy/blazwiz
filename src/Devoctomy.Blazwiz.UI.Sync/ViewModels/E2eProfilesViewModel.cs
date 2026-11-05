using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the end to end encryption section of the settings: two lists, the profiles set up in code (by display
/// name) and the protected profiles the user created (by id), each with its own passphrase. Sets a first passphrase,
/// creates, unlocks, locks, changes the passphrase and deletes. Passphrases are only entered in a dialog, and cleared
/// from the view model once used.
/// </summary>
public partial class E2eProfilesViewModel : ViewModelBase
{
    private static readonly E2eStep[] KeySteps =
    [
        E2eStep.DeriveKey, E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey,
    ];

    private readonly ISyncProtectedProfileService profiles;
    private readonly IE2eProtectedProfileManager manager;
    private readonly IE2eProfilePresenter presenter;
    private readonly IOfflineModeService offlineModeService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;

    private IReadOnlyList<E2eProfileRow> rows = [];
    private string? error;
    private E2eDialogKind? activeDialog;
    private E2eProfileRow? dialogProfile;
    private string passphrase = string.Empty;
    private string confirmPassphrase = string.Empty;
    private string currentPassphrase = string.Empty;
    private string deleteConfirmation = string.Empty;
    private bool acknowledgedNoRecovery;
    private bool isWorking;
    private bool isFinished;
    private string? dialogError;
    private string? createdId;
    private IReadOnlyList<E2eStepItem> steps = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eProfilesViewModel"/> class.
    /// </summary>
    /// <param name="profiles">The user's profiles: create, set up, change and delete.</param>
    /// <param name="manager">Unlocks and locks the profiles.</param>
    /// <param name="presenter">Describes the profiles.</param>
    /// <param name="offlineModeService">Offline mode: the keys in the cloud can't be changed.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public E2eProfilesViewModel(
        ISyncProtectedProfileService profiles,
        IE2eProtectedProfileManager manager,
        IE2eProfilePresenter presenter,
        IOfflineModeService offlineModeService,
        IStringLocalizer<SyncUiText> text,
        IErrorText errorText)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.profiles.ProfilesChanged += OnProfilesChanged;
        this.manager.LockStateChanged += OnLockStateChanged;
        this.offlineModeService.Changed += OnOfflineModeChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the user's profiles are loaded.
    /// </summary>
    public bool IsLoaded => profiles.IsLoaded;

    /// <summary>
    /// Gets a value indicating whether offline mode is on: profiles can be unlocked and locked, but nothing else.
    /// </summary>
    public bool IsOffline => offlineModeService.IsOffline;

    /// <summary>
    /// Gets the profiles set up in code.
    /// </summary>
    public IReadOnlyList<E2eProfileRow> AppProfiles => rows.Where(r => !r.IsCreatedByUser).ToList();

    /// <summary>
    /// Gets the protected profiles the user created.
    /// </summary>
    public IReadOnlyList<E2eProfileRow> CreatedProfiles => rows.Where(r => r.IsCreatedByUser).ToList();

    /// <summary>
    /// Gets a value indicating whether any profile is unlocked, so Lock all can be used.
    /// </summary>
    public bool CanLockAll => rows.Any(r => r.State == E2eProfileState.Unlocked);

    /// <summary>
    /// Gets why the last action outside a dialog did not work, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>
    /// Gets the warning shown before a passphrase is set.
    /// </summary>
    public string NoRecoveryWarning => text["Sync.E2e.NoRecoveryWarning"];

    /// <summary>
    /// Gets the dialog shown, or null.
    /// </summary>
    public E2eDialogKind? ActiveDialog
    {
        get => activeDialog;
        private set => SetProperty(ref activeDialog, value);
    }

    /// <summary>
    /// Gets the profile the dialog is for, or null when creating one.
    /// </summary>
    public E2eProfileRow? DialogProfile
    {
        get => dialogProfile;
        private set => SetProperty(ref dialogProfile, value);
    }

    /// <summary>
    /// Gets the dialog's title.
    /// </summary>
    public string DialogTitle => ActiveDialog switch
    {
        E2eDialogKind.SetPassphrase => text["Sync.E2e.SetPassphrase"],
        E2eDialogKind.Create => text["Sync.E2e.New"],
        E2eDialogKind.Unlock => text["Sync.E2e.Unlock"],
        E2eDialogKind.Delete => text["Sync.E2e.Dialog.Delete"],
        _ => text["Sync.E2e.ChangePassphrase"],
    };

    /// <summary>
    /// Gets the dialog's explanation, naming the profile.
    /// </summary>
    public string DialogText => ActiveDialog switch
    {
        E2eDialogKind.SetPassphrase => text["Sync.E2e.Dialog.SetPassphrase.Text", DialogProfile?.DisplayName ?? string.Empty],
        E2eDialogKind.Create => text["Sync.E2e.Dialog.Create.Text"],
        E2eDialogKind.Unlock => text["Sync.E2e.Dialog.Unlock.Text", DialogProfile?.DisplayName ?? string.Empty],
        E2eDialogKind.Delete => text["Sync.E2e.Dialog.Delete.Text", DialogProfile?.DisplayName ?? string.Empty],
        _ => text["Sync.E2e.Dialog.ChangePassphrase.Text", DialogProfile?.DisplayName ?? string.Empty],
    };

    /// <summary>
    /// Gets what the dialog shows once its change has finished.
    /// </summary>
    public string FinishedText => ActiveDialog switch
    {
        E2eDialogKind.SetPassphrase => text["Sync.E2e.Dialog.PassphraseSet"],
        E2eDialogKind.Create => text["Sync.E2e.Dialog.Created", CreatedId ?? string.Empty],
        E2eDialogKind.Unlock => text["Sync.E2e.Dialog.Unlocked"],
        E2eDialogKind.Delete => text["Sync.E2e.Dialog.Deleted"],
        _ => text["Sync.E2e.Dialog.PassphraseChanged"],
    };

    /// <summary>
    /// Gets a value indicating whether the dialog asks for a new passphrase, with its confirmation and the no recovery
    /// acknowledgement.
    /// </summary>
    public bool AsksForNewPassphrase => ActiveDialog is E2eDialogKind.SetPassphrase or E2eDialogKind.Create or E2eDialogKind.ChangePassphrase;

    /// <summary>
    /// Gets or sets the passphrase being entered (the new one, when changing it).
    /// </summary>
    public string Passphrase
    {
        get => passphrase;
        set => SetProperty(ref passphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the new passphrase entered again, to confirm it.
    /// </summary>
    public string ConfirmPassphrase
    {
        get => confirmPassphrase;
        set => SetProperty(ref confirmPassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the current passphrase, when changing it.
    /// </summary>
    public string CurrentPassphrase
    {
        get => currentPassphrase;
        set => SetProperty(ref currentPassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the id typed to confirm deleting a profile.
    /// </summary>
    public string DeleteConfirmation
    {
        get => deleteConfirmation;
        set => SetProperty(ref deleteConfirmation, value ?? string.Empty);
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
    /// Gets a value indicating whether the dialog's change is running.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => SetProperty(ref isWorking, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog's change has finished successfully.
    /// </summary>
    public bool IsFinished
    {
        get => isFinished;
        private set => SetProperty(ref isFinished, value);
    }

    /// <summary>
    /// Gets why the dialog's change did not work, or null.
    /// </summary>
    public string? DialogError
    {
        get => dialogError;
        private set => SetProperty(ref dialogError, value);
    }

    /// <summary>
    /// Gets the id of the profile just created, or null.
    /// </summary>
    public string? CreatedId
    {
        get => createdId;
        private set => SetProperty(ref createdId, value);
    }

    /// <summary>
    /// Gets the steps of creating a profile's keys, while the dialog runs them.
    /// </summary>
    public IReadOnlyList<E2eStepItem> Steps
    {
        get => steps;
        private set => SetProperty(ref steps, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog is asking for input rather than showing progress or the result.
    /// </summary>
    public bool IsEditing => !IsWorking && !IsFinished && Steps.Count == 0;

    /// <summary>
    /// Gets where a profile stands, as its row says it.
    /// </summary>
    /// <param name="row">The profile.</param>
    /// <returns>The text.</returns>
    public string GetStateText(E2eProfileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return text["Sync.E2e.State." + row.State];
    }

    /// <inheritdoc/>
    public override Task OnInitializedAsync()
    {
        return RefreshAsync();
    }

    /// <summary>
    /// Opens the dialog setting the first passphrase of a profile set up in code.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void OpenSetPassphrase(string name) => OpenDialog(E2eDialogKind.SetPassphrase, name);

    /// <summary>
    /// Opens the dialog creating a protected profile.
    /// </summary>
    [RelayCommand]
    public void OpenCreate() => OpenDialog(E2eDialogKind.Create, null);

    /// <summary>
    /// Opens the dialog unlocking a profile with its passphrase.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void OpenUnlock(string name) => OpenDialog(E2eDialogKind.Unlock, name);

    /// <summary>
    /// Opens the dialog changing a profile's passphrase.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void OpenChangePassphrase(string name) => OpenDialog(E2eDialogKind.ChangePassphrase, name);

    /// <summary>
    /// Opens the dialog deleting a protected profile the user created.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void OpenDelete(string name) => OpenDialog(E2eDialogKind.Delete, name);

    /// <summary>
    /// Locks a profile.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void Lock(string name)
    {
        if (FindRow(name) is { } row)
        {
            manager.Lock(new E2eKeyScope(row.Id));
        }
    }

    /// <summary>
    /// Locks every profile.
    /// </summary>
    [RelayCommand]
    public void LockAll()
    {
        manager.LockAll();
    }

    /// <summary>
    /// Lets this device unlock an unlocked profile with the passkey, when its settings allow it.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task EnableDeviceUnlockAsync(string name)
    {
        return FindRow(name) is { } row ? RunActionAsync(() => profiles.EnableDeviceUnlockAsync(new E2eKeyScope(row.Id))) : Task.CompletedTask;
    }

    /// <summary>
    /// Unlocks a profile with the passkey.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task UnlockWithDeviceAsync(string name)
    {
        return FindRow(name) is { } row ? RunActionAsync(() => profiles.UnlockWithDeviceAsync(new E2eKeyScope(row.Id))) : Task.CompletedTask;
    }

    /// <summary>
    /// Closes the dialog. Ignored while its change is running.
    /// </summary>
    [RelayCommand]
    public void CloseDialog()
    {
        if (IsWorking)
        {
            return;
        }

        ClearInputs();
        ActiveDialog = null;
        DialogProfile = null;
        Steps = [];
        IsFinished = false;
        DialogError = null;
        CreatedId = null;
        RefreshDialog();
    }

    /// <summary>
    /// Runs the dialog's change, once its fields are valid.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ConfirmDialogAsync()
    {
        if (IsWorking || ActiveDialog == null)
        {
            return Task.CompletedTask;
        }

        DialogError = Validate();
        if (DialogError != null)
        {
            return Task.CompletedTask;
        }

        var scope = DialogProfile is { } row ? new E2eKeyScope(row.Id) : null;
        var value = Passphrase;
        var current = CurrentPassphrase;
        return ActiveDialog switch
        {
            E2eDialogKind.SetPassphrase => RunStepsAsync(reporter => profiles.SetUpAsync(scope!, value, reporter)),
            E2eDialogKind.Create => RunStepsAsync(async reporter => CreatedId = (await profiles.CreateAsync(value, reporter)).Id),
            E2eDialogKind.Unlock => RunAsync(() => manager.UnlockAsync(scope!, value)),
            E2eDialogKind.Delete => RunAsync(() => profiles.DeleteAsync(scope!)),
            _ => RunAsync(() => profiles.ChangePassphraseAsync(scope!, current, value)),
        };
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            profiles.ProfilesChanged -= OnProfilesChanged;
            manager.LockStateChanged -= OnLockStateChanged;
            offlineModeService.Changed -= OnOfflineModeChanged;
        }

        base.Dispose(disposing);
    }

    private E2eProfileRow? FindRow(string name) => rows.FirstOrDefault(r => r.Name == name);

    private void OpenDialog(E2eDialogKind kind, string? name)
    {
        if (IsWorking)
        {
            return;
        }

        ClearInputs();
        DialogError = null;
        IsFinished = false;
        CreatedId = null;
        Steps = [];
        DialogProfile = name == null ? null : FindRow(name);
        ActiveDialog = kind;
        RefreshDialog();
    }

    private string? Validate()
    {
        if (ActiveDialog == E2eDialogKind.Delete)
        {
            return string.Equals(DeleteConfirmation.Trim(), DialogProfile?.Id, StringComparison.OrdinalIgnoreCase)
                ? null
                : text["Sync.E2e.Error.DeleteConfirmationDiffers"].Value;
        }

        if (ActiveDialog == E2eDialogKind.ChangePassphrase && string.IsNullOrEmpty(CurrentPassphrase))
        {
            return text["Sync.E2e.Error.CurrentPassphraseRequired"];
        }

        if (string.IsNullOrEmpty(Passphrase))
        {
            return text["Sync.E2e.Error.PassphraseRequired"];
        }

        if (!AsksForNewPassphrase)
        {
            return null;
        }

        if (!string.Equals(Passphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            return text["Sync.E2e.Error.PassphrasesDiffer"];
        }

        return AcknowledgedNoRecovery ? null : text["Sync.E2e.Error.AcknowledgeNoRecovery"].Value;
    }

    private async Task RunAsync(Func<Task> action)
    {
        DialogError = null;
        IsWorking = true;
        RefreshDialog();
        try
        {
            await action();
            ClearInputs();
            IsFinished = true;
        }
        catch (Exception ex)
        {
            DialogError = Describe(ex);
        }
        finally
        {
            IsWorking = false;
            RefreshDialog();
            await RefreshAsync();
        }
    }

    private async Task RunStepsAsync(Func<IProgress<E2eProgress>, Task> action)
    {
        DialogError = null;
        Steps = KeySteps.Select(s => new E2eStepItem { Step = s, Label = text["Sync.E2e.Step." + s], State = E2eStepState.Pending }).ToList();
        IsWorking = true;
        RefreshDialog();
        try
        {
            await action(new ProgressReporter(this));
            ClearInputs();
            IsFinished = true;
        }
        catch (Exception ex)
        {
            if (Steps.All(s => s.State == E2eStepState.Pending))
            {
                // Refused before anything started (e.g. too short): back to the form to correct it.
                Steps = [];
            }
            else
            {
                FailRunningStep();
            }

            DialogError = Describe(ex);
        }
        finally
        {
            IsWorking = false;
            RefreshDialog();
            await RefreshAsync();
        }
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        Error = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }

        await RefreshAsync();
    }

    private string Describe(Exception ex) => ex is E2eWrongPassphraseException ? text["Sync.E2e.Error.WrongPassphrase"] : errorText.Describe(ex);

    private void ApplyProgress(E2eProgress progress)
    {
        var items = Steps.ToList();
        var item = items.FirstOrDefault(s => s.Step == progress.Step);
        if (item == null)
        {
            return;
        }

        item.State = progress.Completed ? E2eStepState.Done : E2eStepState.Running;
        item.Detail = progress.Total > 0
            ? text["Sync.E2e.Step.Progress", progress.Done, progress.Total].Value
            : null;
        Steps = items;
    }

    private void FailRunningStep()
    {
        var items = Steps.ToList();
        var running = items.LastOrDefault(s => s.State == E2eStepState.Running)
            ?? items.FirstOrDefault(s => s.State == E2eStepState.Pending);
        if (running != null)
        {
            running.State = E2eStepState.Failed;
        }

        Steps = items;
    }

    private void ClearInputs()
    {
        Passphrase = string.Empty;
        ConfirmPassphrase = string.Empty;
        CurrentPassphrase = string.Empty;
        DeleteConfirmation = string.Empty;
        AcknowledgedNoRecovery = false;
    }

    private void RefreshDialog()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(DialogTitle));
        OnPropertyChanged(nameof(DialogText));
        OnPropertyChanged(nameof(FinishedText));
        OnPropertyChanged(nameof(AsksForNewPassphrase));
    }

    private async Task RefreshAsync()
    {
        rows = await presenter.GetProfilesAsync();
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(AppProfiles));
        OnPropertyChanged(nameof(CreatedProfiles));
        OnPropertyChanged(nameof(CanLockAll));
    }

    private async void OnProfilesChanged(object? sender, EventArgs e)
    {
        await RefreshSafelyAsync();
    }

    private async void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        await RefreshSafelyAsync();
    }

    private async void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        OnPropertyChanged(nameof(IsOffline));
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

    /// <summary>
    /// Shows progress as it is reported, on the caller's context (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class ProgressReporter(E2eProfilesViewModel owner)
        : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value)
        {
            owner.ApplyProgress(value);
        }
    }
}
