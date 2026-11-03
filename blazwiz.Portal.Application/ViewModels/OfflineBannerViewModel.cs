using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Offline.Interfaces;
using blazwiz.Core.Offline.Models;
using blazwiz.Portal.Application.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the banner shown across the app in offline mode, saying why and what it means.
/// </summary>
public partial class OfflineBannerViewModel : ViewModelBase
{
    private readonly IOfflineModeService offlineModeService;
    private readonly IStringLocalizer<UiText> text;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineBannerViewModel"/> class.
    /// </summary>
    /// <param name="offlineModeService">Offline mode.</param>
    /// <param name="text">The UI's text.</param>
    public OfflineBannerViewModel(IOfflineModeService offlineModeService, IStringLocalizer<UiText> text)
    {
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.offlineModeService.Changed += OnChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the banner is shown.
    /// </summary>
    public bool IsVisible => offlineModeService.IsOffline;

    /// <summary>
    /// Gets the banner text.
    /// </summary>
    public string Message => offlineModeService.Reason == OfflineEntryReason.SignInUnreachable ? text["Offline.Banner.Unreachable"] : text["Offline.Banner.Chosen"];

    /// <summary>
    /// Leaves offline mode, which ends the offline session so the user signs in online.
    /// </summary>
    [RelayCommand]
    public void SignInOnline()
    {
        offlineModeService.Exit();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            offlineModeService.Changed -= OnChanged;
        }

        base.Dispose(disposing);
    }

    private void OnChanged(object? sender, bool isOffline)
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Message));
    }
}
