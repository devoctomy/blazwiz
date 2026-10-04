using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Offline.Interfaces;
using blazwiz.Core.Offline.Models;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the banner shown across the app in offline mode, saying why and what it means.
/// </summary>
public partial class OfflineBannerViewModel : ViewModelBase
{
    /// <summary>
    /// The banner text after choosing offline mode.
    /// </summary>
    public const string ChosenMessage = "You're in offline mode. Nothing is synced or sent to the cloud, and you'll be signed out 15 minutes after going offline.";

    /// <summary>
    /// The banner text after signing in offline because the sign-in service could not be reached.
    /// </summary>
    public const string UnreachableMessage = "Couldn't reach the sign-in service, so you're signed in offline. Sync and some features are disabled, and the session lasts 15 minutes.";

    private readonly IOfflineModeService offlineModeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineBannerViewModel"/> class.
    /// </summary>
    /// <param name="offlineModeService">Offline mode.</param>
    public OfflineBannerViewModel(IOfflineModeService offlineModeService)
    {
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.offlineModeService.Changed += OnChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the banner is shown.
    /// </summary>
    public bool IsVisible => offlineModeService.IsOffline;

    /// <summary>
    /// Gets the banner text.
    /// </summary>
    public string Message => offlineModeService.Reason == OfflineEntryReason.SignInUnreachable ? UnreachableMessage : ChosenMessage;

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
