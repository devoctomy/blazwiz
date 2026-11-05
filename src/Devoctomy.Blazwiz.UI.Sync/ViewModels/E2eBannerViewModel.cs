using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the banner shown across the app while a profile set up in code has no passphrase yet, so its files
/// cannot be used until the user sets one in Settings. Locked profiles are unlocked where their content is shown.
/// </summary>
public partial class E2eBannerViewModel : ViewModelBase
{
    private readonly ISyncProtectedProfileService profiles;
    private readonly ISyncProfileDisplayNames displayNames;
    private readonly INavigationService navigationService;
    private readonly IStringLocalizer<SyncUiText> text;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eBannerViewModel"/> class.
    /// </summary>
    /// <param name="profiles">The user's profiles.</param>
    /// <param name="displayNames">The names profiles are shown by.</param>
    /// <param name="navigationService">Opens Settings.</param>
    /// <param name="text">The sync UI's text.</param>
    public E2eBannerViewModel(
        ISyncProtectedProfileService profiles,
        ISyncProfileDisplayNames displayNames,
        INavigationService navigationService,
        IStringLocalizer<SyncUiText> text)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.profiles.ProfilesChanged += OnProfilesChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the banner is shown: a profile set up in code has no passphrase yet.
    /// </summary>
    public bool IsVisible => NotSetUp.Count > 0;

    /// <summary>
    /// Gets the banner's text, naming the profiles that need a passphrase.
    /// </summary>
    public string Message => IsVisible
        ? text["Sync.E2e.Banner.NeedsPassphrase", string.Join(", ", NotSetUp.Select(p => displayNames.GetDisplayName(p)))]
        : string.Empty;

    private IReadOnlyList<string> NotSetUp => profiles.Profiles.Where(p => !p.IsCreatedByUser && !p.IsSetUp).Select(p => p.Name).ToList();

    /// <summary>
    /// Opens Settings, where the passphrase is set.
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
        }

        base.Dispose(disposing);
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Message));
    }
}
