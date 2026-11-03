using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Models.Settings;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the main application shell: sidebar, theme, current user and logout. Modules add their own parts
/// (banners, status pills, overlays) to the shell's regions, and start and stop their work through
/// <see cref="ISessionLifecycle"/>.
/// </summary>
public partial class MainLayoutViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IThemeService themeService;
    private readonly IUserSettingsService userSettingsService;
    private readonly INavigationMenuService navigationMenuService;
    private readonly INavigationService navigationService;
    private readonly IUserDisplayService userDisplayService;
    private readonly ISessionLifecycle sessionLifecycle;
    private readonly IShellRegistry shellRegistry;
    private readonly IShellContentService shellContentService;
    private readonly AppSettings appSettings;

    private bool isReady;
    private bool isEndingSession;
    private bool isSidebarCollapsed;
    private bool isMobileMenuOpen;
    private UserSession? currentUser;
    private int contentVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainLayoutViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="userSettingsService">The user settings service.</param>
    /// <param name="navigationMenuService">The navigation menu service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="userDisplayService">The user display service.</param>
    /// <param name="sessionLifecycle">Lets modules start and stop their work with the session.</param>
    /// <param name="shellRegistry">The components modules add to the shell.</param>
    /// <param name="shellContentService">Lets modules ask for the page to be shown again from scratch.</param>
    /// <param name="appSettings">The application settings.</param>
    public MainLayoutViewModel(
        IAuthenticationService authService,
        IThemeService themeService,
        IUserSettingsService userSettingsService,
        INavigationMenuService navigationMenuService,
        INavigationService navigationService,
        IUserDisplayService userDisplayService,
        ISessionLifecycle sessionLifecycle,
        IShellRegistry shellRegistry,
        IShellContentService shellContentService,
        AppSettings appSettings)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        this.userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        this.navigationMenuService = navigationMenuService ?? throw new ArgumentNullException(nameof(navigationMenuService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
        this.sessionLifecycle = sessionLifecycle ?? throw new ArgumentNullException(nameof(sessionLifecycle));
        this.shellRegistry = shellRegistry ?? throw new ArgumentNullException(nameof(shellRegistry));
        this.shellContentService = shellContentService ?? throw new ArgumentNullException(nameof(shellContentService));
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

        this.themeService.ThemeChanged += OnThemeChanged;
        this.authService.SessionEnded += OnSessionEnded;
        this.shellContentService.ReloadRequested += OnReloadRequested;
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

    /// <summary>
    /// Gets a number that changes when the page content must be shown again from scratch (it is used as the
    /// content's key), e.g. after a storage folder is reconnected, so pages reload their files.
    /// </summary>
    public int ContentVersion
    {
        get => contentVersion;
        private set => SetProperty(ref contentVersion, value);
    }

    /// <summary>
    /// Gets the components shown above the page content.
    /// </summary>
    public IReadOnlyList<Type> Banners => shellRegistry.GetComponents(ShellRegion.Banners);

    /// <summary>
    /// Gets the components shown in the top bar, before the theme toggle.
    /// </summary>
    public IReadOnlyList<Type> TopBarActions => shellRegistry.GetComponents(ShellRegion.TopBarActions);

    /// <summary>
    /// Gets the components floating over the content.
    /// </summary>
    public IReadOnlyList<Type> Floating => shellRegistry.GetComponents(ShellRegion.Floating);

    /// <summary>
    /// Gets the components shown over the whole shell.
    /// </summary>
    public IReadOnlyList<Type> Overlays => shellRegistry.GetComponents(ShellRegion.Overlays);

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
        await sessionLifecycle.ShellLoadingAsync(CurrentUser);
        IsReady = true;

        // Signed in (or opened with a saved session): modules start their work in the background while the page loads.
        _ = sessionLifecycle.SessionStartedAsync(CurrentUser);
    }

    /// <summary>
    /// Toggles the sidebar between expanded and collapsed (icons only) and remembers the choice.
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
    /// Closes the sidebar overlay on small screens, e.g. after navigating.
    /// </summary>
    [RelayCommand]
    public void CloseMobileMenu()
    {
        IsMobileMenuOpen = false;
    }

    /// <summary>
    /// Switches between the light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ToggleThemeAsync()
    {
        return themeService.ToggleThemeAsync();
    }

    /// <summary>
    /// Signs the user out and returns to the login page. Modules stop their work first.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LogoutAsync()
    {
        await sessionLifecycle.SigningOutAsync();
        await authService.LogoutAsync();
        await sessionLifecycle.SignedOutAsync();
        navigationService.NavigateTo("/login");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            themeService.ThemeChanged -= OnThemeChanged;
            authService.SessionEnded -= OnSessionEnded;
            shellContentService.ReloadRequested -= OnReloadRequested;
        }

        base.Dispose(disposing);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ThemeIcon));
    }

    private void OnReloadRequested(object? sender, EventArgs e)
    {
        ContentVersion++;
    }

    // The session expired or could not be renewed, and the auth service has signed out: finish signing out here
    // (modules stop their work and forget what they hold) and return to the login page, which wipes the keys.
    private async void OnSessionEnded(object? sender, EventArgs e)
    {
        if (isEndingSession)
        {
            return;
        }

        isEndingSession = true;
        try
        {
            await LogoutAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw; still let modules forget what they hold, and leave for the login page.
            await sessionLifecycle.SignedOutAsync();
            navigationService.NavigateTo("/login");
        }
        finally
        {
            isEndingSession = false;
        }
    }
}
