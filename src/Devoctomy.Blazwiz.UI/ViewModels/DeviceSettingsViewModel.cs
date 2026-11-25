using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the This device section of the Settings page: the name this device's events give it on the user's
/// other devices (e.g. "You were signed out from Work laptop"), which is also the name it is remembered under. Shown
/// only when the app tells other devices about a sign out everywhere. A remembered device keeps the name it was
/// remembered with, which the sign-in service cannot change, so renaming it asks first and then forgets it: the next
/// sign in asks for a code, and remembers it again under the new name.
/// </summary>
public partial class DeviceSettingsViewModel : ViewModelBase
{
    private readonly IDeviceNameService deviceNameService;
    private readonly IRememberedDeviceService rememberedDevices;
    private readonly ISavedDeviceCredentialService savedDeviceCredential;
    private readonly IErrorText errorText;
    private readonly IStringLocalizer<UiText> text;
    private string name = string.Empty;
    private string savedName = string.Empty;
    private string? errorMessage;
    private string? savedMessage;
    private RememberedDevice? pendingForget;
    private bool isBusy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceSettingsViewModel"/> class.
    /// </summary>
    /// <param name="deviceNameService">The device's name.</param>
    /// <param name="signOutEverywhereNotifier">Says whether other devices are told anything, i.e. whether the name is used.</param>
    /// <param name="rememberedDevices">Says whether this device is remembered, and forgets it when it is renamed.</param>
    /// <param name="savedDeviceCredential">This device's stored credential, which goes when it is forgotten.</param>
    /// <param name="errorText">Describes errors.</param>
    /// <param name="text">The UI's text.</param>
    public DeviceSettingsViewModel(
        IDeviceNameService deviceNameService,
        ISignOutEverywhereNotifier signOutEverywhereNotifier,
        IRememberedDeviceService rememberedDevices,
        ISavedDeviceCredentialService savedDeviceCredential,
        IErrorText errorText,
        IStringLocalizer<UiText> text)
    {
        this.deviceNameService = deviceNameService ?? throw new ArgumentNullException(nameof(deviceNameService));
        ArgumentNullException.ThrowIfNull(signOutEverywhereNotifier);
        this.rememberedDevices = rememberedDevices ?? throw new ArgumentNullException(nameof(rememberedDevices));
        this.savedDeviceCredential = savedDeviceCredential ?? throw new ArgumentNullException(nameof(savedDeviceCredential));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        IsVisible = signOutEverywhereNotifier.HasHandlers;
    }

    /// <summary>
    /// Gets a value indicating whether the section is shown: only when other devices are told about this one.
    /// </summary>
    public bool IsVisible { get; }

    /// <summary>
    /// Gets the longest name allowed.
    /// </summary>
    public int MaxLength => DeviceNameService.MaxLength;

    /// <summary>
    /// Gets or sets the name being edited.
    /// </summary>
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>
    /// Gets the error from the last save, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Gets the confirmation from the last save, if any.
    /// </summary>
    public string? SavedMessage
    {
        get => savedMessage;
        private set => SetProperty(ref savedMessage, value);
    }

    /// <summary>
    /// Gets the warning shown before renaming a remembered device, or null when none is waiting to be confirmed.
    /// </summary>
    public string? ForgetWarning => pendingForget == null ? null : text["Settings.Device.Forget.Text", Name.Trim()].Value;

    /// <summary>
    /// Gets a value indicating whether a rename is being confirmed: the device is being forgotten.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        if (IsVisible)
        {
            Name = await deviceNameService.GetNameAsync();
            savedName = Name;
        }
    }

    /// <summary>
    /// Saves the name. A new name for a remembered device waits for <see cref="ConfirmForgetAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        SavedMessage = null;
        SetPendingForget(null);
        var trimmed = (Name ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > DeviceNameService.MaxLength || trimmed.Any(char.IsControl))
        {
            ErrorMessage = text["Settings.Device.Invalid", DeviceNameService.MaxLength];
            return;
        }

        Name = trimmed;
        if (!string.Equals(trimmed, savedName, StringComparison.Ordinal) && await FindThisRememberedDeviceAsync() is { } device)
        {
            SetPendingForget(device);
            return;
        }

        await StoreAsync(trimmed);
    }

    /// <summary>
    /// Renames a remembered device: forgets it, drops its stored credential, and saves the new name.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ConfirmForgetAsync()
    {
        if (pendingForget is not { } device || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await rememberedDevices.ForgetAsync(device.Key);
            await savedDeviceCredential.DropAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Still remembered under the old name: the name is not changed either.
            ErrorMessage = errorText.Describe(ex);
            return;
        }
        finally
        {
            SetPendingForget(null);
            IsBusy = false;
        }

        await StoreAsync(Name);
    }

    /// <summary>
    /// Keeps the device remembered and its name as it was.
    /// </summary>
    [RelayCommand]
    public void CancelForget()
    {
        SetPendingForget(null);
        Name = savedName;
    }

    private async Task<RememberedDevice?> FindThisRememberedDeviceAsync()
    {
        try
        {
            return (await rememberedDevices.ListAsync()).FirstOrDefault(d => d.IsThisDevice);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline or unreachable: the name is saved here, and the device stays remembered under its old name.
            return null;
        }
    }

    private async Task StoreAsync(string value)
    {
        await deviceNameService.SetNameAsync(value);
        savedName = value;
        SavedMessage = text["Settings.Device.Saved"];
    }

    private void SetPendingForget(RememberedDevice? device)
    {
        pendingForget = device;
        OnPropertyChanged(nameof(ForgetWarning));
    }
}
