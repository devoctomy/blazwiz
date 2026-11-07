using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the unlock panel, shown in place of a profile's content while it is locked: its passphrase, and the
/// passkey when this device can use it. A profile with no passphrase yet points to Settings, where it is set. Online, the
/// profile is synced before it is unlocked, so what it shows once open is up to date. A user with a passkey on this
/// device can choose to unlock it with biometrics from then on; they are asked for the passkey whenever its key is not
/// in memory.
/// </summary>
public partial class E2eUnlockPanelViewModel : ViewModelBase
{
    private readonly ISyncProtectedProfileService profiles;
    private readonly IE2eProtectedProfileManager manager;
    private readonly IE2eProfilePresenter presenter;
    private readonly INavigationService navigationService;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;
    private readonly ISyncScheduler scheduler;
    private readonly IPasskeyKeysService passkeys;
    private readonly IToastService toasts;

    private string? profile;
    private E2eProfileRow? row;
    private string passphrase = string.Empty;
    private string? error;
    private bool isWorking;
    private bool rememberBiometrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eUnlockPanelViewModel"/> class.
    /// </summary>
    /// <param name="profiles">The user's profiles.</param>
    /// <param name="manager">Unlocks the profile.</param>
    /// <param name="presenter">Describes the profile.</param>
    /// <param name="navigationService">Opens Settings.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    /// <param name="scheduler">Syncs the profile before it is unlocked.</param>
    /// <param name="passkeys">Asks for the passkey when its key is needed.</param>
    /// <param name="toasts">Says if biometrics could not be turned on.</param>
    public E2eUnlockPanelViewModel(
        ISyncProtectedProfileService profiles,
        IE2eProtectedProfileManager manager,
        IE2eProfilePresenter presenter,
        INavigationService navigationService,
        IStringLocalizer<SyncUiText> text,
        IErrorText errorText,
        ISyncScheduler scheduler,
        IPasskeyKeysService passkeys,
        IToastService toasts)
    {
        this.passkeys = passkeys ?? throw new ArgumentNullException(nameof(passkeys));
        this.toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.profiles.ProfilesChanged += OnProfilesChanged;
        this.manager.LockStateChanged += OnLockStateChanged;
    }

    /// <summary>
    /// Gets the profile, or null until it is known.
    /// </summary>
    public E2eProfileRow? Profile
    {
        get => row;
        private set => SetProperty(ref row, value);
    }

    /// <summary>
    /// Gets a value indicating whether the profile is unlocked, so its content is shown.
    /// </summary>
    public bool IsUnlocked => Profile?.State == E2eProfileState.Unlocked;

    /// <summary>
    /// Gets a value indicating whether the profile is locked, so its passphrase is asked for.
    /// </summary>
    public bool IsLocked => Profile?.State == E2eProfileState.Locked;

    /// <summary>
    /// Gets a value indicating whether the profile has no passphrase yet, so the panel points to Settings.
    /// </summary>
    public bool NeedsPassphrase => Profile?.State == E2eProfileState.NotSetUp;

    /// <summary>
    /// Gets what the panel says.
    /// </summary>
    public string Message => Profile switch
    {
        { State: E2eProfileState.Locked } p => text["Sync.E2e.Panel.Locked", p.DisplayName],
        { State: E2eProfileState.NotSetUp } p => text["Sync.E2e.Panel.NeedsPassphrase", p.DisplayName],
        { } => string.Empty,
        null => text["Sync.E2e.Status.Checking"],
    };

    /// <summary>
    /// Gets or sets the passphrase being entered.
    /// </summary>
    public string Passphrase
    {
        get => passphrase;
        set => SetProperty(ref passphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets why unlocking did not work, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether, once unlocked with the passphrase, the profile is unlocked with
    /// biometrics on this device from then on. Offered when <see cref="E2eProfileRow.CanSetUpDeviceUnlock"/>.
    /// </summary>
    public bool RememberBiometrics
    {
        get => rememberBiometrics;
        set => SetProperty(ref rememberBiometrics, value);
    }

    /// <summary>
    /// Gets a value indicating whether the profile is being unlocked.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => SetProperty(ref isWorking, value);
    }

    /// <summary>
    /// Sets the profile the panel is for.
    /// </summary>
    /// <param name="name">The sync profile's name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task SetProfileAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name == profile)
        {
            return Task.CompletedTask;
        }

        profile = name;
        Passphrase = string.Empty;
        Error = null;
        return RefreshAsync();
    }

    /// <summary>
    /// Unlocks the profile with the passphrase entered.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task UnlockAsync()
    {
        if (string.IsNullOrEmpty(Passphrase))
        {
            Error = text["Sync.E2e.Error.PassphraseRequired"];
            return Task.CompletedTask;
        }

        var value = Passphrase;
        var remember = RememberBiometrics && Profile?.CanSetUpDeviceUnlock == true;
        return RunAsync(async scope =>
        {
            await manager.UnlockAsync(scope, value);
            if (remember)
            {
                await TurnOnBiometricsAsync(scope);
            }
        });
    }

    /// <summary>
    /// Unlocks the profile with the passkey.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task UnlockWithDeviceAsync()
    {
        return RunAsync(async scope =>
        {
            if (!await passkeys.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey))
            {
                // Cancelled, or no passkey here: the passphrase is still asked for.
                throw new E2ePassphraseRequiredException();
            }

            await profiles.UnlockWithDeviceAsync(scope);
        });
    }

    /// <summary>
    /// Opens Settings, where a first passphrase is set.
    /// </summary>
    [RelayCommand]
    public void OpenSettings()
    {
        navigationService.NavigateTo("/settings");
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

    private async Task RunAsync(Func<E2eKeyScope, Task> unlock)
    {
        if (IsWorking || Profile is not { } current)
        {
            return;
        }

        Error = null;
        IsWorking = true;
        try
        {
            await SyncFirstAsync();
            await unlock(new E2eKeyScope(current.Id));
            Passphrase = string.Empty;
        }
        catch (Exception ex)
        {
            Error = ex is E2eWrongPassphraseException ? text["Sync.E2e.Error.WrongPassphrase"] : errorText.Describe(ex);
        }
        finally
        {
            IsWorking = false;
            await RefreshAsync();
        }
    }

    private async Task TurnOnBiometricsAsync(E2eKeyScope scope)
    {
        try
        {
            if (await passkeys.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey))
            {
                await profiles.EnableDeviceUnlockAsync(scope);
                RememberBiometrics = false;
                return;
            }
        }
        catch (Exception)
        {
            // Said below; the profile is unlocked all the same.
        }

        toasts.Show(text["Sync.E2e.Error.BiometricsNotSetUp"], ToastTone.Warning);
    }

    private async Task SyncFirstAsync()
    {
        if (profile == null)
        {
            return;
        }

        try
        {
            // Skipped offline or while sync is turned off; a failed pass is reported by the sync status and log.
            await scheduler.SyncProfileAsync(profile);
        }
        catch (Exception)
        {
            // Unlocking goes ahead with the files this device has.
        }
    }

    private async Task RefreshAsync()
    {
        Profile = profile == null ? null : await presenter.GetProfileAsync(profile);
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(NeedsPassphrase));
        OnPropertyChanged(nameof(Message));
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
