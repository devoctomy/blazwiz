using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class MainLayoutViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IThemeService> mockTheme = new();
    private readonly Mock<IUserSettingsService> mockUserSettings = new();
    private readonly Mock<INavigationMenuService> mockMenu = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly AppSettings appSettings = new();
    private readonly UserSettings userSettings = new();
    private readonly UserSession session = new() { Username = "bob@example.com", DisplayName = "Bob Hoskins" };

    public MainLayoutViewModelTests()
    {
        mockUserSettings.Setup(x => x.GetSettingsAsync()).ReturnsAsync(userSettings);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(null!, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, appSettings)).ParamName);
        Assert.Equal("themeService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, null!, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, appSettings)).ParamName);
        Assert.Equal("userSettingsService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, null!, mockMenu.Object, mockNavigation.Object, appSettings)).ParamName);
        Assert.Equal("navigationMenuService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, null!, mockNavigation.Object, appSettings)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, null!, appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenMenuItems_WhenGettingNavigationItems_ThenReturnsMenuServiceItems()
    {
        // Arrange
        var items = new List<NavigationItem> { new() { Text = "Home" } };
        mockMenu.Setup(x => x.GetMainItems()).Returns(items);
        var sut = CreateSut();

        // Act & Assert
        Assert.Same(items, sut.NavigationItems);
        Assert.Same(appSettings, sut.AppSettings);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenShellIsReadyWithUserAndSidebarState()
    {
        // Arrange
        userSettings.SidebarCollapsed = true;
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsReady);
        Assert.Same(session, sut.CurrentUser);
        Assert.Equal("BH", sut.UserInitials);
        Assert.True(sut.IsSidebarCollapsed);
        mockTheme.Verify(x => x.InitializeAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenRedirectsToLoginAndIsNotReady()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsReady);
        Assert.Null(sut.CurrentUser);
        Assert.Equal("?", sut.UserInitials);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    [Fact]
    public async Task GivenExpandedSidebar_WhenToggleSidebar_ThenCollapsesAndSavesPreference()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleSidebarCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsSidebarCollapsed);
        Assert.True(userSettings.SidebarCollapsed);
        mockUserSettings.Verify(x => x.SaveSettingsAsync(userSettings), Times.Once);
    }

    [Fact]
    public async Task GivenCollapsedSidebar_WhenToggleSidebar_ThenExpands()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ToggleSidebarAsync();

        // Act
        await sut.ToggleSidebarAsync();

        // Assert
        Assert.False(sut.IsSidebarCollapsed);
        Assert.False(userSettings.SidebarCollapsed);
    }

    [Fact]
    public void GivenClosedMobileMenu_WhenToggleMobileMenu_ThenOpensAndThenCloses()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.ToggleMobileMenuCommand.Execute(null);
        Assert.True(sut.IsMobileMenuOpen);
        sut.ToggleMobileMenuCommand.Execute(null);
        Assert.False(sut.IsMobileMenuOpen);
    }

    [Fact]
    public void GivenOpenMobileMenu_WhenCloseMobileMenu_ThenCloses()
    {
        // Arrange
        var sut = CreateSut();
        sut.ToggleMobileMenu();

        // Act
        sut.CloseMobileMenuCommand.Execute(null);

        // Assert
        Assert.False(sut.IsMobileMenuOpen);
    }

    [Fact]
    public async Task GivenShell_WhenToggleTheme_ThenDelegatesToThemeService()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleThemeCommand.ExecuteAsync(null);

        // Assert
        mockTheme.Verify(x => x.ToggleThemeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true, "bi-sun")]
    [InlineData(false, "bi-moon-stars")]
    public void GivenTheme_WhenGettingThemeIcon_ThenReturnsIconForOtherTheme(bool isDark, string expected)
    {
        // Arrange
        mockTheme.SetupGet(x => x.IsDark).Returns(isDark);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(expected, sut.ThemeIcon);
    }

    [Fact]
    public void GivenThemeChanges_WhenThemeChangedRaised_ThenThemeIconChangeIsNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(MainLayoutViewModel.ThemeIcon), changed);
    }

    [Fact]
    public void GivenDisposed_WhenThemeChangedRaised_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Empty(changed);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenLogout_ThenLogsOutAndNavigatesToLogin()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LogoutCommand.ExecuteAsync(null);

        // Assert
        mockAuth.Verify(x => x.LogoutAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    private MainLayoutViewModel CreateSut() => new(
        mockAuth.Object,
        mockTheme.Object,
        mockUserSettings.Object,
        mockMenu.Object,
        mockNavigation.Object,
        appSettings);
}
