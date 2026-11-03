using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

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
    private readonly IOfflineModeService offlineModeService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;

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
    /// <param name="offlineModeService">Offline mode: the keys in the cloud can't be reached, so nothing can be changed.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public E2eSettingsViewModel(ISyncEncryptionService encryptionService, IOfflineModeService offlineModeService, IStringLocalizer<SyncUiText> text, IErrorText errorText)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.encryptionService.StatusChanged += OnStatusChanged;
        this.offlineModeService.Changed += OnOfflineModeChanged;
    }

    /// <summary>
    /// Gets where encryption stands on this device.
    /// </summary>
    public E2eStatus Status => encryptionService.Status;

    /// <summary>
    /// Gets a value indicating whether offline mode is on.
    /// </summary>
    public bool IsOffline => offlineModeService.IsOffline;

    /// <summary>
    /// Gets the status line. In offline mode it describes this device's own state, and says so.
    /// </summary>
    public string StatusText => IsOffline ? text["Sync.E2e.Status.Offline", OfflineStatusText] : OnlineStatusText;

    /// <summary>
    /// Gets the warning shown before a passphrase is set.
    /// </summary>
    public string NoRecoveryWarning => text["Sync.E2e.NoRecoveryWarning"];

    /// <summary>
    /// Gets a value indicating whether encryption can be turned on.
    /// </summary>
    public bool CanEnable => !IsOffline && Status == E2eStatus.Disabled;

    /// <summary>
    /// Gets a value indicating whether this device can be set up.
    /// </summary>
    public bool CanSetUp => !IsOffline && Status == E2eStatus.NeedsSetup && encryptionService.HasPasskeyKeys;

    /// <summary>
    /// Gets a value indicating whether the passphrase can be changed, or encryption turned off.
    /// </summary>
    public bool CanManage => !IsOffline && Status == E2eStatus.Unlocked;

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

    // Offline the cloud copy of the keys can't be checked: with no copy on this device the status is unknown, which
    // here simply means encryption is off on this device.
    private string OfflineStatusText => Status switch
    {
        E2eStatus.Unknown or E2eStatus.Disabled or E2eStatus.Unavailable => text["Sync.E2e.Status.OffOnThisDevice"],
        _ => OnlineStatusText,
    };

    private string OnlineStatusText => Status switch
    {
        E2eStatus.Unlocked => text["Sync.E2e.Status.Unlocked"],
        E2eStatus.Disabled => text["Sync.E2e.Status.Disabled"],
        E2eStatus.NeedsSetup => text["Sync.E2e.Status.NeedsSetup"],
        E2eStatus.Blocked => text["Sync.E2e.Status.Blocked"],
        E2eStatus.DisabledElsewhere => text["Sync.E2e.Status.DisabledElsewhere"],
        E2eStatus.Unavailable => text["Sync.E2e.Status.Unavailable"],
        E2eStatus.Unsupported => text["Sync.E2e.Status.Unsupported"],
        _ => text["Sync.E2e.Status.Checking"],
    };

    /// <summary>
    /// Gets the description of a step, as the step list shows it.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <returns>The description.</returns>
    public string GetStepLabel(E2eStep step) => step switch
    {
        E2eStep.Sync or E2eStep.GoOffline or E2eStep.CheckExistingKeys or E2eStep.CheckIndex or E2eStep.VerifyPassphrase
            or E2eStep.DeriveKey or E2eStep.CreateKeys or E2eStep.SaveKeys or E2eStep.StoreDeviceKey or E2eStep.EncryptFiles
            or E2eStep.DecryptFiles or E2eStep.DeleteKeys or E2eStep.GoOnline => text["Sync.E2e.Step." + step],
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
            offlineModeService.Changed -= OnOfflineModeChanged;
        }

        base.Dispose(disposing);
    }

    private List<E2eStepItem> CreateSteps(IEnumerable<E2eStep> list) =>
        list.Select(s => new E2eStepItem { Step = s, Label = GetStepLabel(s), State = E2eStepState.Pending }).ToList();

    private string? ValidateNewPassphrase()
    {
        if (Passphrase.Length < MinPassphraseLength)
        {
            return text["Sync.E2e.Error.PassphraseTooShort", MinPassphraseLength];
        }

        if (!string.Equals(Passphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            return text["Sync.E2e.Error.PassphrasesDiffer"];
        }

        return AcknowledgedNoRecovery ? null : text["Sync.E2e.Error.AcknowledgeNoRecovery"].Value;
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
            DialogError = text["Sync.E2e.Error.PassphraseRequired"];
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
            DialogError = text["Sync.E2e.Error.PassphraseRequired"];
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
            DialogError = text["Sync.E2e.Error.CurrentPassphraseRequired"];
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
            DialogError = text["Sync.E2e.Error.WrongPassphrase"];
        }
        catch (Exception ex)
        {
            DialogError = errorText.Describe(ex);
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
            DialogError = errorText.Describe(ex);
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
            ? text["Sync.E2e.Step.Progress", progress.Done, progress.Total].Value
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

    private void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        OnPropertyChanged(nameof(IsOffline));
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
