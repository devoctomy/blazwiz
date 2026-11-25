using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the Remembered devices section of the Settings page: the devices that skip the code from the
/// authenticator app, each of which can be forgotten (e.g. a lost laptop), so it is asked for a code again. Shown only
/// for an online session, since it needs the sign-in service. A device forgotten elsewhere on the page (e.g. by
/// renaming this one) leaves the list too.
/// </summary>
public partial class RememberedDevicesViewModel : ViewModelBase
{
    private readonly IRememberedDeviceService deviceService;
    private readonly IAuthenticationService authService;
    private readonly ISavedDeviceCredentialService savedDeviceCredential;
    private readonly IErrorText errorText;
    private IReadOnlyList<RememberedDevice> devices = [];
    private bool isVisible;
    private bool isBusy;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="RememberedDevicesViewModel"/> class.
    /// </summary>
    /// <param name="deviceService">Lists and forgets the devices.</param>
    /// <param name="authService">The signed-in user.</param>
    /// <param name="savedDeviceCredential">This device's stored credential, which goes when this device is forgotten.</param>
    /// <param name="errorText">Describes errors.</param>
    public RememberedDevicesViewModel(
        IRememberedDeviceService deviceService,
        IAuthenticationService authService,
        ISavedDeviceCredentialService savedDeviceCredential,
        IErrorText errorText)
    {
        this.deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.savedDeviceCredential = savedDeviceCredential ?? throw new ArgumentNullException(nameof(savedDeviceCredential));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.deviceService.Forgotten += OnForgotten;
    }

    /// <summary>
    /// Gets the remembered devices, most recently used first.
    /// </summary>
    public IReadOnlyList<RememberedDevice> Devices
    {
        get => devices;
        private set => SetProperty(ref devices, value);
    }

    /// <summary>
    /// Gets a value indicating whether the section is shown: the session is online and the devices could be listed.
    /// </summary>
    public bool IsVisible
    {
        get => isVisible;
        private set => SetProperty(ref isVisible, value);
    }

    /// <summary>
    /// Gets a value indicating whether a device is being forgotten.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Gets the error from the last change, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        var session = await authService.GetCurrentUserAsync();
        if (session == null || session.IsOffline)
        {
            return;
        }

        try
        {
            Devices = await deviceService.ListAsync();
            IsVisible = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unreachable or refused: the section stays hidden, as it is offline.
        }
    }

    /// <summary>
    /// Forgets a device: its next sign in asks for a code again. Forgetting this one also drops its stored credential.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ForgetAsync(RememberedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await deviceService.ForgetAsync(device.Key);
            if (device.IsThisDevice)
            {
                await savedDeviceCredential.DropAsync();
            }

            Remove(device.Key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = errorText.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            deviceService.Forgotten -= OnForgotten;
        }

        base.Dispose(disposing);
    }

    private void OnForgotten(object? sender, string key) => Remove(key);

    private void Remove(string key)
    {
        if (Devices.Any(d => d.Key == key))
        {
            Devices = Devices.Where(d => d.Key != key).ToList();
        }
    }
}
