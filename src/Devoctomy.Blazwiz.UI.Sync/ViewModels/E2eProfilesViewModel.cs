using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
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

    private static readonly E2eStep[] RotationSteps =
    [
        E2eStep.DeriveKey, E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey, E2eStep.ReencryptFiles, E2eStep.UploadFiles,
    ];

    private static readonly E2eStep[] FinishRotationSteps = [E2eStep.ReencryptFiles, E2eStep.UploadFiles];

    private readonly ISyncProtectedProfileService profiles;
    private readonly IE2eProtectedProfileManager manager;
    private readonly IE2eProfilePresenter presenter;
    private readonly IOfflineModeService offlineModeService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;
    private readonly ISyncKeyRotationService rotation;
    private readonly IE2eRotationSignOut rotationSignOut;

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
    private SyncKeyRotationEstimate? estimate;
    private int filesDone;
    private int filesTotal;
    private bool needsSignOut;
    private Dictionary<E2eStep, (int Done, int Total)> fileSteps = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eProfilesViewModel"/> class.
    /// </summary>
    /// <param name="profiles">The user's profiles: create, set up, change and delete.</param>
    /// <param name="manager">Unlocks and locks the profiles.</param>
    /// <param name="presenter">Describes the profiles.</param>
    /// <param name="offlineModeService">Offline mode: the keys in the cloud can't be changed.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    /// <param name="rotation">Changes a passphrase: syncs, estimates, rotates the keys and encrypts the files again.</param>
    /// <param name="rotationSignOut">Signs every device out once a passphrase change has encrypted the files again.</param>
    public E2eProfilesViewModel(
        ISyncProtectedProfileService profiles,
        IE2eProtectedProfileManager manager,
        IE2eProfilePresenter presenter,
        IOfflineModeService offlineModeService,
        IStringLocalizer<SyncUiText> text,
        IErrorText errorText,
        ISyncKeyRotationService rotation,
        IE2eRotationSignOut rotationSignOut)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.rotation = rotation ?? throw new ArgumentNullException(nameof(rotation));
        this.rotationSignOut = rotationSignOut ?? throw new ArgumentNullException(nameof(rotationSignOut));
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
    public bool CanLockAll => rows.Any(r => r.State is E2eProfileState.Unlocked or E2eProfileState.RotationInProgress);

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
        E2eDialogKind.FinishRotation => text["Sync.E2e.FinishRotation"],
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
        E2eDialogKind.FinishRotation => text["Sync.E2e.Rotation.Interrupted", DialogProfile?.DisplayName ?? string.Empty],
        _ => text["Sync.E2e.Dialog.ChangePassphrase.Text", DialogProfile?.DisplayName ?? string.Empty],
    };

    /// <summary>
    /// Gets the estimate of a passphrase change, once the profile is synced and a file timed; the user confirms it
    /// before anything changes. Null otherwise.
    /// </summary>
    public SyncKeyRotationEstimate? Estimate
    {
        get => estimate;
        private set => SetProperty(ref estimate, value);
    }

    /// <summary>
    /// Gets the estimate as the warning before a passphrase change says it.
    /// </summary>
    public string EstimateText => Estimate is { } e ? DescribeEstimate(e) : string.Empty;

    /// <summary>
    /// Gets the warning shown with the estimate before a passphrase change, or before finishing one.
    /// </summary>
    public string RotationWarning => text["Sync.E2e.Rotation.Warning"];

    /// <summary>
    /// Gets how much of a passphrase change's work on the files is done: files encrypted again, plus files uploaded.
    /// </summary>
    public int FilesDone
    {
        get => filesDone;
        private set => SetProperty(ref filesDone, value);
    }

    /// <summary>
    /// Gets how much work on the files a passphrase change has: files to encrypt again, plus files to upload; 0 before
    /// it knows.
    /// </summary>
    public int FilesTotal
    {
        get => filesTotal;
        private set => SetProperty(ref filesTotal, value);
    }

    /// <summary>
    /// Gets how far a passphrase change is through its files, from 0 to 100.
    /// </summary>
    public int FilesPercent => FilesTotal == 0 ? 0 : (int)Math.Round(100.0 * FilesDone / FilesTotal);

    /// <summary>
    /// Gets a value indicating whether a passphrase change has encrypted the files again but could not sign every
    /// device out, so it is not finished: the sign out is offered again.
    /// </summary>
    public bool NeedsSignOut
    {
        get => needsSignOut;
        private set => SetProperty(ref needsSignOut, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog is waiting for the user to confirm a passphrase change's estimate.
    /// </summary>
    public bool IsConfirmingRotation => Estimate != null && !IsWorking && Steps.Count == 0;

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
    public bool IsEditing => !IsWorking && !IsFinished && Steps.Count == 0 && Estimate == null && !NeedsSignOut;

    /// <summary>
    /// Gets what the dialog says while its change runs without steps to show.
    /// </summary>
    public string WorkingText => ActiveDialog == E2eDialogKind.ChangePassphrase
        ? text["Sync.E2e.Rotation.Preparing"]
        : text["Sync.E2e.Dialog.Working"];

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
    /// Opens the dialog finishing a passphrase change that was cut short.
    /// </summary>
    /// <param name="name">The profile's name.</param>
    [RelayCommand]
    public void OpenFinishRotation(string name) => OpenDialog(E2eDialogKind.FinishRotation, name);

    /// <summary>
    /// Starts the passphrase change whose estimate the user has seen. It runs to the end: the dialog cannot be closed
    /// until every file is encrypted again and uploaded, and then every device, this one included, is signed out.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task StartRotationAsync()
    {
        if (IsWorking || ActiveDialog != E2eDialogKind.ChangePassphrase || Estimate == null || DialogProfile is not { } row)
        {
            return;
        }

        var scope = new E2eKeyScope(row.Id);
        var value = Passphrase;
        var current = CurrentPassphrase;
        Estimate = null;
        if (await RunStepsAsync(RotationSteps, reporter => rotation.RotateAsync(scope, current, value, reporter), finish: false))
        {
            await SignOutEverywhereAsync();
        }
    }

    /// <summary>
    /// Signs every device out again, after a passphrase change whose sign out failed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task RetrySignOutAsync()
    {
        return IsWorking || !NeedsSignOut ? Task.CompletedTask : SignOutEverywhereAsync();
    }

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
        ResetDialogState();
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
            E2eDialogKind.SetPassphrase => RunStepsAsync(KeySteps, reporter => profiles.SetUpAsync(scope!, value, reporter)),
            E2eDialogKind.Create => RunStepsAsync(KeySteps, async reporter => CreatedId = (await profiles.CreateAsync(value, reporter)).Id),
            E2eDialogKind.Unlock => RunAsync(() => manager.UnlockAsync(scope!, value)),
            E2eDialogKind.Delete => RunAsync(() => profiles.DeleteAsync(scope!)),
            E2eDialogKind.FinishRotation => FinishRotationAsync(scope!),
            _ => PrepareRotationAsync(scope!),
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
        ResetDialogState();
        DialogProfile = name == null ? null : FindRow(name);
        ActiveDialog = kind;
        RefreshDialog();
    }

    private void ResetDialogState()
    {
        Steps = [];
        IsFinished = false;
        DialogError = null;
        CreatedId = null;
        Estimate = null;
        NeedsSignOut = false;
        FilesDone = 0;
        FilesTotal = 0;
        fileSteps = [];
    }

    private string DescribeEstimate(SyncKeyRotationEstimate value)
    {
        var duration = value.Duration < TimeSpan.FromMinutes(1)
            ? text["Sync.E2e.Rotation.UnderAMinute"].Value
            : text["Sync.E2e.Rotation.Minutes", (int)Math.Ceiling(value.Duration.TotalMinutes)].Value;
        var megabytes = Math.Round(value.TotalBytes / (1024.0 * 1024.0), 1);
        return text["Sync.E2e.Rotation.Estimate", value.FileCount, megabytes, duration];
    }

    // Step 1 and 2 of a passphrase change: sync, then time a throwaway file. Nothing changes; the estimate is shown and
    // the user confirms it, or cancels.
    private async Task PrepareRotationAsync(E2eKeyScope scope)
    {
        DialogError = null;
        IsWorking = true;
        RefreshDialog();
        try
        {
            Estimate = await rotation.PrepareAsync(scope);
        }
        catch (Exception ex)
        {
            DialogError = Describe(ex);
        }
        finally
        {
            IsWorking = false;
            RefreshDialog();
        }
    }

    private async Task FinishRotationAsync(E2eKeyScope scope)
    {
        if (await RunStepsAsync(FinishRotationSteps, reporter => rotation.FinishAsync(scope, reporter), finish: false))
        {
            await SignOutEverywhereAsync();
        }
    }

    private async Task SignOutEverywhereAsync()
    {
        DialogError = null;
        IsWorking = true;
        RefreshDialog();
        try
        {
            if (await rotationSignOut.SignOutEverywhereAsync())
            {
                // Signed out: the login page is opening.
                NeedsSignOut = false;
                return;
            }

            NeedsSignOut = true;
            DialogError = text["Sync.E2e.Rotation.SignOutFailed"];
        }
        catch (Exception ex)
        {
            NeedsSignOut = true;
            DialogError = Describe(ex);
        }
        finally
        {
            IsWorking = false;
            RefreshDialog();
        }
    }

    private string? Validate()
    {
        if (ActiveDialog == E2eDialogKind.FinishRotation)
        {
            return null;
        }

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

    // Runs a change that reports its steps. With finish false, the dialog is left to the caller once it succeeds (e.g.
    // to sign out). Returns whether it succeeded.
    private async Task<bool> RunStepsAsync(E2eStep[] stepList, Func<IProgress<E2eProgress>, Task> action, bool finish = true)
    {
        DialogError = null;
        Steps = stepList.Select(s => new E2eStepItem { Step = s, Label = text["Sync.E2e.Step." + s], State = E2eStepState.Pending }).ToList();
        IsWorking = true;
        RefreshDialog();
        try
        {
            await action(new ProgressReporter(this));
            ClearInputs();
            IsFinished = finish;
            return true;
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
                // Under way when it failed: the passphrases are not asked for again here.
                ClearInputs();
                FailRunningStep();
            }

            DialogError = Describe(ex);
            return false;
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
        if (progress.Step is E2eStep.ReencryptFiles or E2eStep.UploadFiles)
        {
            fileSteps[progress.Step] = (progress.Done, progress.Total);
            FilesDone = fileSteps.Values.Sum(s => s.Done);
            FilesTotal = fileSteps.Values.Sum(s => s.Total);
            OnPropertyChanged(nameof(FilesPercent));
        }
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
        OnPropertyChanged(nameof(IsConfirmingRotation));
        OnPropertyChanged(nameof(EstimateText));
        OnPropertyChanged(nameof(WorkingText));
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
