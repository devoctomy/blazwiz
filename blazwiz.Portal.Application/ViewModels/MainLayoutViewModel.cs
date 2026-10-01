using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Models.Settings;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the main application shell: sidebar, theme, current user and logout.
/// </summary>
public partial class MainLayoutViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IThemeService themeService;
    private readonly IUserSettingsService userSettingsService;
    private readonly INavigationMenuService navigationMenuService;
    private readonly INavigationService navigationService;
    private readonly IUserDisplayService userDisplayService;
    private readonly AppSettings appSettings;

    private bool isReady;
    private bool isSidebarCollapsed;
    private bool isMobileMenuOpen;
    private UserSession? currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainLayoutViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="userSettingsService">The user settings service.</param>
    /// <param name="navigationMenuService">The navigation menu service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="userDisplayService">The user display service.</param>
    /// <param name="appSettings">The application settings.</param>
    public MainLayoutViewModel(
        IAuthenticationService authService,
        IThemeService themeService,
        IUserSettingsService userSettingsService,
        INavigationMenuService navigationMenuService,
        INavigationService navigationService,
        IUserDisplayService userDisplayService,
        AppSettings appSettings)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        this.userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        this.navigationMenuService = navigationMenuService ?? throw new ArgumentNullException(nameof(navigationMenuService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

        this.themeService.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the user has been verified and the shell can be shown.
    /// </summary>
    public bool IsReady
    {
        get => isReady;
        private set => SetProperty(ref isReady, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sidebar is collapsed to icons only (desktop).
    /// </summary>
    public bool IsSidebarCollapsed
    {
        get => isSidebarCollapsed;
        private set => SetProperty(ref isSidebarCollapsed, value);
    }

    /// <summary>
    /// Gets a value indicating whether the sidebar is open as an overlay (mobile).
    /// </summary>
    public bool IsMobileMenuOpen
    {
        get => isMobileMenuOpen;
        private set => SetProperty(ref isMobileMenuOpen, value);
    }

    /// <summary>
    /// Gets the signed in user.
    /// </summary>
    public UserSession? CurrentUser
    {
        get => currentUser;
        private set
        {
            if (SetProperty(ref currentUser, value))
            {
                OnPropertyChanged(nameof(UserInitials));
            }
        }
    }

    /// <summary>
    /// Gets the initials of the signed in user, for the avatar.
    /// </summary>
    public string UserInitials => userDisplayService.GetInitials(CurrentUser?.DisplayName);

    /// <summary>
    /// Gets the sidebar navigation items.
    /// </summary>
    public IReadOnlyList<NavigationItem> NavigationItems => navigationMenuService.GetMainItems();

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

        CurrentUser = await authService.GetCurrentUserAsync();
        if (CurrentUser == null)
        {
            navigationService.NavigateTo("/login");
            return;
        }

        var settings = await userSettingsService.GetSettingsAsync();
        IsSidebarCollapsed = settings.SidebarCollapsed;
        IsReady = true;
    }

    /// <summary>
    /// Collapses or expands the sidebar and remembers the choice.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleSidebarAsync()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;

        var settings = await userSettingsService.GetSettingsAsync();
        settings.SidebarCollapsed = IsSidebarCollapsed;
        await userSettingsService.SaveSettingsAsync(settings);
    }

    /// <summary>
    /// Opens or closes the sidebar overlay on small screens.
    /// </summary>
    [RelayCommand]
    public void ToggleMobileMenu()
    {
        IsMobileMenuOpen = !IsMobileMenuOpen;
    }

    /// <summary>
    /// Closes the sidebar overlay on small screens.
    /// </summary>
    [RelayCommand]
    public void CloseMobileMenu()
    {
        IsMobileMenuOpen = false;
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ToggleThemeAsync()
    {
        return themeService.ToggleThemeAsync();
    }

    /// <summary>
    /// Signs the user out and returns to the login page.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LogoutAsync()
    {
        await authService.LogoutAsync();
        navigationService.NavigateTo("/login");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            themeService.ThemeChanged -= OnThemeChanged;
        }

        base.Dispose(disposing);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ThemeIcon));
    }
}
