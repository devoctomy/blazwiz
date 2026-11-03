using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Text.Interfaces;
using blazwiz.Portal.Application.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the banner shown across the app while end to end encryption needs the user: locked (no passkey keys
/// in this session), this device not set up, or turned off on another device.
/// </summary>
public partial class E2eBannerViewModel : ViewModelBase
{
    private readonly ISyncEncryptionService encryptionService;
    private readonly IPasskeyUnlockService passkeyUnlockService;
    private readonly ISyncScheduler syncScheduler;
    private readonly INavigationService navigationService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;

    private bool hasSavedPasskey;
    private bool isWorking;
    private bool isResolveDialogOpen;
    private bool resolveNeedsPassphrase;
    private string resolvePassphrase = string.Empty;
    private string? error;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eBannerViewModel"/> class.
    /// </summary>
    /// <param name="encryptionService">End to end encryption of the synced files.</param>
    /// <param name="passkeyUnlockService">Uses the saved passkey again to get its PRF keys back.</param>
    /// <param name="syncScheduler">Syncs once encryption allows it.</param>
    /// <param name="navigationService">Opens the settings.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public E2eBannerViewModel(
        ISyncEncryptionService encryptionService,
        IPasskeyUnlockService passkeyUnlockService,
        ISyncScheduler syncScheduler,
        INavigationService navigationService,
        IStringLocalizer<SyncUiText> text,
        IErrorText errorText)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.passkeyUnlockService = passkeyUnlockService ?? throw new ArgumentNullException(nameof(passkeyUnlockService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.encryptionService.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Gets where encryption stands on this device.
    /// </summary>
    public E2eStatus Status => encryptionService.Status;

    /// <summary>
    /// Gets a value indicating whether the banner is shown.
    /// </summary>
    public bool IsVisible => Status is E2eStatus.Blocked or E2eStatus.NeedsSetup or E2eStatus.DisabledElsewhere;

    /// <summary>
    /// Gets the banner text.
    /// </summary>
    public string Message => Status switch
    {
        E2eStatus.Blocked => text["Sync.E2e.Banner.Blocked"],
        E2eStatus.NeedsSetup when !encryptionService.HasPasskeyKeys => text["Sync.E2e.Banner.NeedsPasskeyAndSetup"],
        E2eStatus.NeedsSetup => text["Sync.E2e.Banner.NeedsSetup"],
        E2eStatus.DisabledElsewhere => text["Sync.E2e.Banner.DisabledElsewhere"],
        _ => string.Empty,
    };

    /// <summary>
    /// Gets a value indicating whether to offer unlocking with the passkey.
    /// </summary>
    public bool CanUnlockWithPasskey => hasSavedPasskey && !encryptionService.HasPasskeyKeys && IsVisible;

    /// <summary>
    /// Gets a value indicating whether to offer setting this device up.
    /// </summary>
    public bool CanSetUp => Status == E2eStatus.NeedsSetup && encryptionService.HasPasskeyKeys;

    /// <summary>
    /// Gets a value indicating whether to offer finishing turning encryption off. The passkey is not required: with no
    /// changes to decrypt the keys are not needed, and otherwise the passphrase can unlock them.
    /// </summary>
    public bool CanResolve => Status == E2eStatus.DisabledElsewhere;

    /// <summary>
    /// Gets a value indicating whether an action is running.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => SetProperty(ref isWorking, value);
    }

    /// <summary>
    /// Gets a value indicating whether the confirmation to finish turning encryption off is shown.
    /// </summary>
    public bool IsResolveDialogOpen
    {
        get => isResolveDialogOpen;
        private set => SetProperty(ref isResolveDialogOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether finishing turning encryption off needs the passphrase, so the confirmation asks
    /// for it.
    /// </summary>
    public bool ResolveNeedsPassphrase
    {
        get => resolveNeedsPassphrase;
        private set => SetProperty(ref resolveNeedsPassphrase, value);
    }

    /// <summary>
    /// Gets or sets the passphrase entered in the confirmation, when it is needed.
    /// </summary>
    public string ResolvePassphrase
    {
        get => resolvePassphrase;
        set => SetProperty(ref resolvePassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets why the last action did not work, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        try
        {
            hasSavedPasskey = await passkeyUnlockService.CanUnlockAsync();
        }
        catch (Exception)
        {
            hasSavedPasskey = false;
        }

        Refresh();
    }

    /// <summary>
    /// Asks for the passkey, then unlocks with this device's keys and syncs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task UnlockWithPasskeyAsync()
    {
        await RunAsync(async () =>
        {
            if (!await passkeyUnlockService.UnlockAsync())
            {
                Error = text["Sync.E2e.Error.PasskeyNotUsed"];
                return;
            }

            if (await encryptionService.InitializeAsync() == E2eStatus.Unlocked)
            {
                await syncScheduler.SyncNowAsync();
            }
        });
    }

    /// <summary>
    /// Opens the settings, where this device is set up.
    /// </summary>
    [RelayCommand]
    public void OpenSettings()
    {
        navigationService.NavigateTo("/settings");
    }

    /// <summary>
    /// Shows the confirmation to finish turning encryption off, asking for the passphrase if it will be needed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task OpenResolveDialogAsync()
    {
        Error = null;
        ResolvePassphrase = string.Empty;
        try
        {
            ResolveNeedsPassphrase = await encryptionService.ResolveNeedsPassphraseAsync();
        }
        catch (Exception)
        {
            // Asked for when resolving, if it turns out to be needed.
            ResolveNeedsPassphrase = false;
        }

        IsResolveDialogOpen = true;
    }

    /// <summary>
    /// Closes the confirmation. Ignored while it runs.
    /// </summary>
    [RelayCommand]
    public void CloseResolveDialog()
    {
        if (!IsWorking)
        {
            ResolvePassphrase = string.Empty;
            IsResolveDialogOpen = false;
        }
    }

    /// <summary>
    /// Finishes turning encryption off on this device: decrypts its changes not yet synced, forgets its keys, syncs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ResolveAsync()
    {
        if (ResolveNeedsPassphrase && string.IsNullOrEmpty(ResolvePassphrase))
        {
            Error = text["Sync.E2e.Error.PassphraseRequired"];
            return;
        }

        var passphrase = ResolveNeedsPassphrase ? ResolvePassphrase : null;
        ResolvePassphrase = string.Empty;
        await RunAsync(async () =>
        {
            try
            {
                await encryptionService.ResolveDisabledElsewhereAsync(passphrase);
                IsResolveDialogOpen = false;
            }
            catch (E2ePassphraseRequiredException ex)
            {
                ResolveNeedsPassphrase = true;
                Error = errorText.Describe(ex);
            }
            catch (E2eWrongPassphraseException)
            {
                Error = text["Sync.E2e.Error.WrongPassphrase"];
            }
        });
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

    private async Task RunAsync(Func<Task> action)
    {
        if (IsWorking)
        {
            return;
        }

        Error = null;
        IsWorking = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error = errorText.Describe(ex);
        }
        finally
        {
            IsWorking = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(CanUnlockWithPasskey));
        OnPropertyChanged(nameof(CanSetUp));
        OnPropertyChanged(nameof(CanResolve));
    }

    private void OnStatusChanged(object? sender, E2eStatus e)
    {
        Refresh();
    }
}
