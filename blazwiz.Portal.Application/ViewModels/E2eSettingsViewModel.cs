using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Portal.Models.Sync;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the end to end encryption section of the settings: turn it on or off, set up this device, change
/// the passphrase. Passphrases are only entered in a dialog, and cleared from the view model as soon as they are used.
/// </summary>
public partial class E2eSettingsViewModel : ViewModelBase
{
    /// <summary>
    /// The shortest passphrase accepted.
    /// </summary>
    public const int MinPassphraseLength = 8;

    /// <summary>
    /// The warning shown before a passphrase is set.
    /// </summary>
    public const string NoRecoveryWarning = "There is no passphrase recovery. If you forget your passphrase, nobody can decrypt your notes, including us.";

    private static readonly E2eStep[] EnableSteps =
    [
        E2eStep.Sync, E2eStep.GoOffline, E2eStep.CheckExistingKeys, E2eStep.CheckIndex, E2eStep.DeriveKey,
        E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey, E2eStep.EncryptFiles, E2eStep.GoOnline,
    ];

    private static readonly E2eStep[] DisableSteps =
    [
        E2eStep.VerifyPassphrase, E2eStep.Sync, E2eStep.GoOffline, E2eStep.DecryptFiles, E2eStep.DeleteKeys, E2eStep.GoOnline,
    ];

    private readonly ISyncEncryptionService encryptionService;

    private E2eDialogKind? activeDialog;
    private string passphrase = string.Empty;
    private string confirmPassphrase = string.Empty;
    private string currentPassphrase = string.Empty;
    private bool acknowledgedNoRecovery;
    private bool isWorking;
    private bool isFinished;
    private string? dialogError;
    private IReadOnlyList<E2eStepItem> steps = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eSettingsViewModel"/> class.
    /// </summary>
    /// <param name="encryptionService">End to end encryption of the synced files.</param>
    public E2eSettingsViewModel(ISyncEncryptionService encryptionService)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.encryptionService.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Gets where encryption stands on this device.
    /// </summary>
    public E2eStatus Status => encryptionService.Status;

    /// <summary>
    /// Gets the status line.
    /// </summary>
    public string StatusText => Status switch
    {
        E2eStatus.Unlocked => "On. Your notes are encrypted on this device before they are synced.",
        E2eStatus.Disabled => "Off.",
        E2eStatus.NeedsSetup => "On, but this device has not been set up yet. Enter your passphrase to read and sync your notes here.",
        E2eStatus.Blocked => "On, but locked: use your passkey to unlock it.",
        E2eStatus.DisabledElsewhere => "Turned off on another device.",
        E2eStatus.Unavailable => "Off. Sign in with your passkey to turn it on: it needs the hardware PRF key your passkey provides.",
        E2eStatus.Unsupported => "Not available with this sign in.",
        _ => "Checking…",
    };

    /// <summary>
    /// Gets a value indicating whether encryption can be turned on.
    /// </summary>
    public bool CanEnable => Status == E2eStatus.Disabled;

    /// <summary>
    /// Gets a value indicating whether this device can be set up.
    /// </summary>
    public bool CanSetUp => Status == E2eStatus.NeedsSetup && encryptionService.HasPasskeyKeys;

    /// <summary>
    /// Gets a value indicating whether the passphrase can be changed, or encryption turned off.
    /// </summary>
    public bool CanManage => Status == E2eStatus.Unlocked;

    /// <summary>
    /// Gets the dialog shown, or null.
    /// </summary>
    public E2eDialogKind? ActiveDialog
    {
        get => activeDialog;
        private set => SetProperty(ref activeDialog, value);
    }

    /// <summary>
    /// Gets or sets the passphrase being entered (the new one, when changing it).
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
    /// Gets or sets the current passphrase, when changing it.
    /// </summary>
    public string CurrentPassphrase
    {
        get => currentPassphrase;
        set => SetProperty(ref currentPassphrase, value ?? string.Empty);
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
    /// Gets the steps of turning encryption on or off, while the dialog runs it.
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
    /// Gets the description of a step, as the step list shows it.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <returns>The description.</returns>
    public static string GetStepLabel(E2eStep step) => step switch
    {
        E2eStep.Sync => "Sync every note to this device",
        E2eStep.GoOffline => "Pause syncing",
        E2eStep.CheckExistingKeys => "Check encryption is not already on",
        E2eStep.CheckIndex => "Check no notes are already encrypted",
        E2eStep.VerifyPassphrase => "Check your passphrase",
        E2eStep.DeriveKey => "Derive your passphrase key (Argon2id)",
        E2eStep.CreateKeys => "Create your master key and file key",
        E2eStep.SaveKeys => "Save your keys to your account",
        E2eStep.StoreDeviceKey => "Secure this device with your passkey",
        E2eStep.EncryptFiles => "Encrypt your notes",
        E2eStep.DecryptFiles => "Decrypt your notes",
        E2eStep.DeleteKeys => "Delete your keys",
        E2eStep.GoOnline => "Resume syncing",
        _ => step.ToString(),
    };

    /// <summary>
    /// Opens a dialog with empty fields.
    /// </summary>
    /// <param name="kind">The dialog.</param>
    [RelayCommand]
    public void OpenDialog(E2eDialogKind kind)
    {
        if (IsWorking)
        {
            return;
        }

        ClearInputs();
        DialogError = null;
        IsFinished = false;
        Steps = [];
        ActiveDialog = kind;
        OnPropertyChanged(nameof(IsEditing));
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
        Steps = [];
        IsFinished = false;
        DialogError = null;
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>
    /// Runs the dialog's change, once its fields are valid.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ConfirmDialogAsync()
    {
        return IsWorking || ActiveDialog == null
            ? Task.CompletedTask
            : ActiveDialog switch
            {
                E2eDialogKind.Enable => EnableAsync(),
                E2eDialogKind.SetUp => SetUpAsync(),
                E2eDialogKind.ChangePassphrase => ChangePassphraseAsync(),
                _ => DisableAsync(),
            };
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            encryptionService.StatusChanged -= OnStatusChanged;
        }

        base.Dispose(disposing);
    }

    private static List<E2eStepItem> CreateSteps(IEnumerable<E2eStep> list) =>
        list.Select(s => new E2eStepItem { Step = s, Label = GetStepLabel(s), State = E2eStepState.Pending }).ToList();

    private string? ValidateNewPassphrase()
    {
        if (Passphrase.Length < MinPassphraseLength)
        {
            return $"Use a passphrase of at least {MinPassphraseLength} characters.";
        }

        if (!string.Equals(Passphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            return "The passphrases do not match.";
        }

        return AcknowledgedNoRecovery ? null : "Confirm you understand there is no passphrase recovery.";
    }

    private async Task EnableAsync()
    {
        DialogError = ValidateNewPassphrase();
        if (DialogError != null)
        {
            return;
        }

        var value = Passphrase;
        ClearInputs();
        await RunStepsAsync(EnableSteps, reporter => encryptionService.EnableAsync(value, reporter));
    }

    private async Task DisableAsync()
    {
        if (string.IsNullOrEmpty(Passphrase))
        {
            DialogError = "Enter your passphrase.";
            return;
        }

        var value = Passphrase;
        ClearInputs();
        await RunStepsAsync(DisableSteps, reporter => encryptionService.DisableAsync(value, reporter));
    }

    private async Task SetUpAsync()
    {
        if (string.IsNullOrEmpty(Passphrase))
        {
            DialogError = "Enter your passphrase.";
            return;
        }

        var value = Passphrase;
        ClearInputs();
        await RunAsync(() => encryptionService.SetUpDeviceAsync(value));
    }

    private async Task ChangePassphraseAsync()
    {
        if (string.IsNullOrEmpty(CurrentPassphrase))
        {
            DialogError = "Enter your current passphrase.";
            return;
        }

        DialogError = ValidateNewPassphrase();
        if (DialogError != null)
        {
            return;
        }

        var current = CurrentPassphrase;
        var value = Passphrase;
        ClearInputs();
        await RunAsync(() => encryptionService.ChangePassphraseAsync(current, value));
    }

    private async Task RunAsync(Func<Task> action)
    {
        DialogError = null;
        IsWorking = true;
        OnPropertyChanged(nameof(IsEditing));
        try
        {
            await action();
            IsFinished = true;
        }
        catch (E2eWrongPassphraseException)
        {
            DialogError = "That passphrase is not correct. Try again.";
        }
        catch (Exception ex)
        {
            DialogError = ex.Message;
        }
        finally
        {
            IsWorking = false;
            OnPropertyChanged(nameof(IsEditing));
            RefreshStatus();
        }
    }

    private async Task RunStepsAsync(IEnumerable<E2eStep> list, Func<IProgress<E2eProgress>, Task> action)
    {
        DialogError = null;
        Steps = CreateSteps(list);
        IsWorking = true;
        OnPropertyChanged(nameof(IsEditing));
        try
        {
            await action(new ProgressReporter(this));
            IsFinished = true;
        }
        catch (Exception ex)
        {
            FailRunningStep();
            DialogError = ex is E2eWrongPassphraseException ? "That passphrase is not correct." : ex.Message;
        }
        finally
        {
            IsWorking = false;
            OnPropertyChanged(nameof(IsEditing));
            RefreshStatus();
        }
    }

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
            ? string.Create(CultureInfo.InvariantCulture, $"{progress.Done} of {progress.Total}")
            : null;
        Steps = items;
    }

    private void FailRunningStep()
    {
        var items = Steps.ToList();
        var running = items.LastOrDefault(s => s.State == E2eStepState.Running && s.Step != E2eStep.GoOnline)
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
        AcknowledgedNoRecovery = false;
    }

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanEnable));
        OnPropertyChanged(nameof(CanSetUp));
        OnPropertyChanged(nameof(CanManage));
    }

    private void OnStatusChanged(object? sender, E2eStatus e)
    {
        RefreshStatus();
    }

    /// <summary>
    /// Shows progress as it is reported, on the caller's context (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class ProgressReporter(E2eSettingsViewModel owner)
        : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value)
        {
            owner.ApplyProgress(value);
        }
    }
}
