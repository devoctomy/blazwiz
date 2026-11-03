using Blazing.Mvvm.ComponentModel;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the current user's profile page.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IUserDisplayService userDisplayService;
    private UserSession? user;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="userDisplayService">The user display service.</param>
    public ProfileViewModel(IAuthenticationService authService, IUserDisplayService userDisplayService)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
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
            }
        }
    }

    /// <summary>
    /// Gets the initials of the signed in user, for the avatar.
    /// </summary>
    public string Initials => userDisplayService.GetInitials(User?.DisplayName);

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        User = await authService.GetCurrentUserAsync();
    }
}
