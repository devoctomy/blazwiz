using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the current user's profile page.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private UserSession? user;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    public ProfileViewModel(IAuthenticationService authService)
    {
        this.authService = ValidationHelpers.VerifiedAssignment(authService);
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
    public string Initials => UserDisplayHelpers.GetInitials(User?.DisplayName);

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        User = await authService.GetCurrentUserAsync();
    }
}
