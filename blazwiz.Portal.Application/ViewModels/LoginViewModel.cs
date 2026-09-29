using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the login page.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly INavigationService navigationService;
    private readonly IThemeService themeService;
    private readonly AppSettings appSettings;

    private string username = string.Empty;
    private string password = string.Empty;
    private bool isBusy;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="appSettings">The application settings.</param>
    public LoginViewModel(
        IAuthenticationService authService,
        INavigationService navigationService,
        IThemeService themeService,
        AppSettings appSettings)
    {
        this.authService = ValidationHelpers.VerifiedAssignment(authService);
        this.navigationService = ValidationHelpers.VerifiedAssignment(navigationService);
        this.themeService = ValidationHelpers.VerifiedAssignment(themeService);
        this.appSettings = ValidationHelpers.VerifiedAssignment(appSettings);
    }

    /// <summary>
    /// Gets or sets the username entered by the user.
    /// </summary>
    public string Username
    {
        get => username;
        set => SetProperty(ref username, value);
    }

    /// <summary>
    /// Gets or sets the password entered by the user.
    /// </summary>
    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    /// <summary>
    /// Gets a value indicating whether a login is in progress.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Gets the error message from the last login attempt, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Gets the Bootstrap icon for the theme toggle button.
    /// </summary>
    public string ThemeIcon => themeService.IsDark ? "bi-sun" : "bi-moon-stars";

    /// <summary>
    /// Gets the application settings.
    /// </summary>
    public AppSettings AppSettings => appSettings;

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await themeService.InitializeAsync();
        OnPropertyChanged(nameof(ThemeIcon));

        if (await authService.GetCurrentUserAsync() != null)
        {
            navigationService.NavigateTo("/");
        }
    }

    /// <summary>
    /// Attempts to log in with the entered credentials.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await authService.LoginAsync(Username, Password);
            if (result.Success)
            {
                Password = string.Empty;
                navigationService.NavigateTo("/");
            }
            else
            {
                ErrorMessage = result.Error ?? "Login failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleThemeAsync()
    {
        await themeService.ToggleThemeAsync();
        OnPropertyChanged(nameof(ThemeIcon));
    }
}
