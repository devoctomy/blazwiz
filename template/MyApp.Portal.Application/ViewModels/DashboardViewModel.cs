using Blazing.Mvvm.ComponentModel;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using MyApp.Portal.Application.Services.Interfaces;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the dashboard (home) page.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly IAuthenticationService authService;
    private readonly IGreetingService greetingService;
    private readonly INavigationMenuService navigationMenuService;
    private readonly AppSettings appSettings;

    private string greeting = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="greetingService">The greeting service.</param>
    /// <param name="navigationMenuService">The navigation menu service.</param>
    /// <param name="appSettings">The application settings.</param>
    public DashboardViewModel(
        IAuthenticationService authService,
        IGreetingService greetingService,
        INavigationMenuService navigationMenuService,
        AppSettings appSettings)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.greetingService = greetingService ?? throw new ArgumentNullException(nameof(greetingService));
        this.navigationMenuService = navigationMenuService ?? throw new ArgumentNullException(nameof(navigationMenuService));
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    /// <summary>
    /// Gets the greeting for the signed in user.
    /// </summary>
    public string Greeting
    {
        get => greeting;
        private set => SetProperty(ref greeting, value);
    }

    /// <summary>
    /// Gets the pages to show as quick links (every navigation item except the dashboard itself).
    /// </summary>
    public IReadOnlyList<NavigationItem> QuickLinks => navigationMenuService
        .GetMainItems()
        .Where(i => !string.IsNullOrEmpty(i.Href))
        .ToList();

    /// <summary>
    /// Gets the application settings.
    /// </summary>
    public AppSettings AppSettings => appSettings;

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        var user = await authService.GetCurrentUserAsync();
        Greeting = greetingService.GetGreeting(user?.DisplayName);
    }
}
