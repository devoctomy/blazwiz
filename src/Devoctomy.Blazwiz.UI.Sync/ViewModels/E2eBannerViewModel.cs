using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the banner shown across the app while the profiles set up in code are locked on this device, so their
/// files cannot be read or written until the user enters their passphrase in Settings.
/// </summary>
public partial class E2eBannerViewModel : ViewModelBase
{
    private readonly ISyncEncryptionService encryptionService;
    private readonly INavigationService navigationService;
    private readonly IStringLocalizer<SyncUiText> text;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eBannerViewModel"/> class.
    /// </summary>
    /// <param name="encryptionService">The passphrase of the profiles set up in code.</param>
    /// <param name="navigationService">Opens the settings.</param>
    /// <param name="text">The sync UI's text.</param>
    public E2eBannerViewModel(
        ISyncEncryptionService encryptionService,
        INavigationService navigationService,
        IStringLocalizer<SyncUiText> text)
    {
        this.encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.encryptionService.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Gets where the profiles set up in code stand on this device.
    /// </summary>
    public E2eStatus Status => encryptionService.Status;

    /// <summary>
    /// Gets a value indicating whether the banner is shown.
    /// </summary>
    public bool IsVisible => Status == E2eStatus.NeedsSetup;

    /// <summary>
    /// Gets the banner text.
    /// </summary>
    public string Message => IsVisible ? text["Sync.E2e.Banner.NeedsSetup"] : string.Empty;

    /// <summary>
    /// Gets a value indicating whether to offer entering the passphrase.
    /// </summary>
    public bool CanSetUp => IsVisible;

    /// <summary>
    /// Opens the settings, where the passphrase is entered.
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
            encryptionService.StatusChanged -= OnStatusChanged;
        }

        base.Dispose(disposing);
    }

    private void OnStatusChanged(object? sender, E2eStatus status)
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(CanSetUp));
    }
}
