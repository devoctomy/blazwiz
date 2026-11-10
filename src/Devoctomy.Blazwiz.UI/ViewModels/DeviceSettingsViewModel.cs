using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the This device section of the Settings page: the name this device's events give it on the user's
/// other devices (e.g. "You were signed out from Work laptop"). Shown only when the app tells other devices about a
/// sign out everywhere.
/// </summary>
public partial class DeviceSettingsViewModel : ViewModelBase
{
    private readonly IDeviceNameService deviceNameService;
    private readonly IStringLocalizer<UiText> text;
    private string name = string.Empty;
    private string? errorMessage;
    private string? savedMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceSettingsViewModel"/> class.
    /// </summary>
    /// <param name="deviceNameService">The device's name.</param>
    /// <param name="signOutEverywhereNotifier">Says whether other devices are told anything, i.e. whether the name is used.</param>
    /// <param name="text">The UI's text.</param>
    public DeviceSettingsViewModel(IDeviceNameService deviceNameService, ISignOutEverywhereNotifier signOutEverywhereNotifier, IStringLocalizer<UiText> text)
    {
        this.deviceNameService = deviceNameService ?? throw new ArgumentNullException(nameof(deviceNameService));
        ArgumentNullException.ThrowIfNull(signOutEverywhereNotifier);
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

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        if (IsVisible)
        {
            Name = await deviceNameService.GetNameAsync();
        }
    }

    /// <summary>
    /// Saves the name.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        SavedMessage = null;
        var trimmed = (Name ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > DeviceNameService.MaxLength || trimmed.Any(char.IsControl))
        {
            ErrorMessage = text["Settings.Device.Invalid", DeviceNameService.MaxLength];
            return;
        }

        await deviceNameService.SetNameAsync(trimmed);
        Name = trimmed;
        SavedMessage = text["Settings.Device.Saved"];
    }
}
