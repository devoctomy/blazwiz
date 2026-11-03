using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Application.Shell.Interfaces;
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
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<ISessionLifecycle> mockLifecycle = new();
    private readonly Mock<IShellRegistry> mockShell = new();
    private readonly ShellContentService shellContent = new();
    private readonly AppSettings appSettings = new();
    private readonly UserSettings userSettings = new();
    private readonly UserSession session = new() { Username = "bob@example.com", DisplayName = "Bob Hoskins" };

    public MainLayoutViewModelTests()
    {
        mockUserSettings.Setup(x => x.GetSettingsAsync()).ReturnsAsync(userSettings);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        mockUserDisplay.Setup(x => x.GetInitials("Bob Hoskins")).Returns("BH");
        mockUserDisplay.Setup(x => x.GetInitials(null)).Returns("?");
        mockShell.Setup(x => x.GetComponents(It.IsAny<ShellRegion>())).Returns([]);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(null!, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("themeService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, null!, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("userSettingsService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, null!, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("navigationMenuService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, null!, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, null!, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("userDisplayService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, null!, mockLifecycle.Object, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("sessionLifecycle", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, null!, mockShell.Object, shellContent, appSettings)).ParamName);
        Assert.Equal("shellRegistry", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, null!, shellContent, appSettings)).ParamName);
        Assert.Equal("shellContentService", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, null!, appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new MainLayoutViewModel(mockAuth.Object, mockTheme.Object, mockUserSettings.Object, mockMenu.Object, mockNavigation.Object, mockUserDisplay.Object, mockLifecycle.Object, mockShell.Object, shellContent, null!)).ParamName);
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
    public void GivenComponentsInRegions_WhenGettingRegions_ThenEachRegionsComponentsReturned()
    {
        // Arrange
        mockShell.Setup(x => x.GetComponents(ShellRegion.Banners)).Returns([typeof(string)]);
        mockShell.Setup(x => x.GetComponents(ShellRegion.TopBarActions)).Returns([typeof(int)]);
        mockShell.Setup(x => x.GetComponents(ShellRegion.Floating)).Returns([typeof(long)]);
        mockShell.Setup(x => x.GetComponents(ShellRegion.Overlays)).Returns([typeof(bool)]);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal([typeof(string)], sut.Banners);
        Assert.Equal([typeof(int)], sut.TopBarActions);
        Assert.Equal([typeof(long)], sut.Floating);
        Assert.Equal([typeof(bool)], sut.Overlays);
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
    public async Task GivenSignedInUser_WhenInitialized_ThenModulesLoadBeforeTheShellShowsAndStartAfter()
    {
        // Arrange
        var sut = CreateSut();
        var readyWhenLoading = (bool?)null;
        var readyWhenStarted = (bool?)null;
        mockLifecycle.Setup(x => x.ShellLoadingAsync(session)).Callback(() => readyWhenLoading = sut.IsReady).Returns(Task.CompletedTask);
        mockLifecycle.Setup(x => x.SessionStartedAsync(session)).Callback(() => readyWhenStarted = sut.IsReady).Returns(Task.CompletedTask);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(readyWhenLoading);
        Assert.True(readyWhenStarted);
    }

    [Fact]
    public async Task GivenModulesStillStarting_WhenInitialized_ThenShellReadyWithoutWaiting()
    {
        // Arrange
        mockLifecycle.Setup(x => x.SessionStartedAsync(session)).Returns(new TaskCompletionSource().Task);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsReady);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenRedirectsToLoginAndNoModuleStarts()
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
        mockLifecycle.Verify(x => x.ShellLoadingAsync(It.IsAny<UserSession>()), Times.Never);
        mockLifecycle.Verify(x => x.SessionStartedAsync(It.IsAny<UserSession>()), Times.Never);
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
    public void GivenModuleAsksForReload_WhenReloadRequested_ThenContentVersionChanges()
    {
        // Arrange
        var sut = CreateSut();
        var version = sut.ContentVersion;

        // Act
        shellContent.RequestReload();

        // Assert
        Assert.Equal(version + 1, sut.ContentVersion);
    }

    [Fact]
    public void GivenDisposed_WhenThemeChangedOrReloadRequested_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);
        shellContent.RequestReload();

        // Assert
        Assert.Empty(changed);
    }

    [Fact]
    public async Task GivenSignedIn_WhenLogout_ThenModulesStopBeforeSigningOutAndForgetAfterThenLoginShown()
    {
        // Arrange
        var order = new List<string>();
        mockLifecycle.Setup(x => x.SigningOutAsync()).Callback(() => order.Add("signing out")).Returns(Task.CompletedTask);
        mockAuth.Setup(x => x.LogoutAsync()).Callback(() => order.Add("logout")).Returns(Task.CompletedTask);
        mockLifecycle.Setup(x => x.SignedOutAsync()).Callback(() => order.Add("signed out")).Returns(Task.CompletedTask);
        mockNavigation.Setup(x => x.NavigateTo("/login")).Callback(() => order.Add("login"));

        // Act
        await CreateSut().LogoutCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["signing out", "logout", "signed out", "login"], order);
    }

    [Fact]
    public void GivenSessionEnded_WhenRaised_ThenSignedOutAndLoginShown()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        mockAuth.Raise(x => x.SessionEnded += null, mockAuth.Object, EventArgs.Empty);

        // Assert
        mockLifecycle.Verify(x => x.SigningOutAsync(), Times.Once);
        mockLifecycle.Verify(x => x.SignedOutAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    [Fact]
    public void GivenSignOutFails_WhenSessionEnded_ThenModulesStillForgetAndLoginShown()
    {
        // Arrange
        mockAuth.Setup(x => x.LogoutAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        using var sut = CreateSut();

        // Act
        mockAuth.Raise(x => x.SessionEnded += null, mockAuth.Object, EventArgs.Empty);

        // Assert
        mockLifecycle.Verify(x => x.SignedOutAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    [Fact]
    public void GivenDisposed_WhenSessionEnded_ThenNothingHappens()
    {
        // Arrange
        var sut = CreateSut();
        sut.Dispose();

        // Act
        mockAuth.Raise(x => x.SessionEnded += null, mockAuth.Object, EventArgs.Empty);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    private MainLayoutViewModel CreateSut() => new(
        mockAuth.Object,
        mockTheme.Object,
        mockUserSettings.Object,
        mockMenu.Object,
        mockNavigation.Object,
        mockUserDisplay.Object,
        mockLifecycle.Object,
        mockShell.Object,
        shellContent,
        appSettings);
}
