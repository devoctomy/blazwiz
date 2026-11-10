using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the current user's profile page, including signing out everywhere.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IUserDisplayService userDisplayService;
    private readonly ISignOutEverywhereNotifier signOutEverywhereNotifier;
    private readonly ISessionLifecycle sessionLifecycle;
    private readonly INavigationService navigationService;
    private readonly IStringLocalizer<UiText> text;
    private UserSession? user;
    private bool isConfirmingSignOutEverywhere;
    private bool isSigningOutEverywhere;
    private string? signOutEverywhereError;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="userDisplayService">The user display service.</param>
    /// <param name="signOutEverywhereNotifier">Says whether other devices can be told at once.</param>
    /// <param name="sessionLifecycle">Lets modules stop their work when the user signs out.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="text">The UI's text.</param>
    public ProfileViewModel(
        IAuthenticationService authService,
        IUserDisplayService userDisplayService,
        ISignOutEverywhereNotifier signOutEverywhereNotifier,
        ISessionLifecycle sessionLifecycle,
        INavigationService navigationService,
        IStringLocalizer<UiText> text)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
        this.signOutEverywhereNotifier = signOutEverywhereNotifier ?? throw new ArgumentNullException(nameof(signOutEverywhereNotifier));
        this.sessionLifecycle = sessionLifecycle ?? throw new ArgumentNullException(nameof(sessionLifecycle));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Gets the signed in user.
    /// </summary>
    public UserSession? User
    {
        get => user;
        private set
        {
            if (SetProperty(ref user, value))
            {
                OnPropertyChanged(nameof(Initials));
                OnPropertyChanged(nameof(CanSignOutEverywhere));
            }
        }
    }

    /// <summary>
    /// Gets the initials of the signed in user, for the avatar.
    /// </summary>
    public string Initials => userDisplayService.GetInitials(User?.DisplayName);

    /// <summary>
    /// Gets a value indicating whether "Sign out everywhere" is offered: the app tells the user's other open devices
    /// (it has user events), and the session is online (signing out everywhere needs the sign-in service).
    /// </summary>
    public bool CanSignOutEverywhere => signOutEverywhereNotifier.HasHandlers && User is { IsOffline: false };

    /// <summary>
    /// Gets a value indicating whether the confirmation is shown.
    /// </summary>
    public bool IsConfirmingSignOutEverywhere
    {
        get => isConfirmingSignOutEverywhere;
        private set => SetProperty(ref isConfirmingSignOutEverywhere, value);
    }

    /// <summary>
    /// Gets a value indicating whether signing out everywhere is under way.
    /// </summary>
    public bool IsSigningOutEverywhere
    {
        get => isSigningOutEverywhere;
        private set => SetProperty(ref isSigningOutEverywhere, value);
    }

    /// <summary>
    /// Gets why signing out everywhere failed, if it did.
    /// </summary>
    public string? SignOutEverywhereError
    {
        get => signOutEverywhereError;
        private set => SetProperty(ref signOutEverywhereError, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        User = await authService.GetCurrentUserAsync();
    }

    /// <summary>
    /// Shows the confirmation for signing out everywhere.
    /// </summary>
    [RelayCommand]
    public void AskSignOutEverywhere()
    {
        SignOutEverywhereError = null;
        IsConfirmingSignOutEverywhere = true;
    }

    /// <summary>
    /// Closes the confirmation without doing anything.
    /// </summary>
    [RelayCommand]
    public void CancelSignOutEverywhere()
    {
        IsConfirmingSignOutEverywhere = false;
    }

    /// <summary>
    /// Signs the user out on every device, this one included, and returns to the login page. When the sign-in service
    /// cannot be reached nothing changes, and the error says so.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SignOutEverywhereAsync()
    {
        if (IsSigningOutEverywhere)
        {
            return;
        }

        IsSigningOutEverywhere = true;
        SignOutEverywhereError = null;
        try
        {
            if (!await authService.SignOutEverywhereAsync(SignOutReason.UserRequested))
            {
                IsConfirmingSignOutEverywhere = false;
                SignOutEverywhereError = text["Profile.SignOutEverywhere.Failed"];
                return;
            }

            // Signed out with the sign-in service; finish here as a sign out does: modules stop and forget what they
            // hold, then the login page.
            await sessionLifecycle.SigningOutAsync();
            await sessionLifecycle.SignedOutAsync();
            navigationService.NavigateTo("/login");
        }
        finally
        {
            IsSigningOutEverywhere = false;
        }
    }
}
