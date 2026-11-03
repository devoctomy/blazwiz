using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Moq;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.ViewModels;

namespace MyApp.Portal.Application.UnitTests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IGreetingService> mockGreeting = new();
    private readonly Mock<INavigationMenuService> mockMenu = new();
    private readonly AppSettings appSettings = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(null!, mockGreeting.Object, mockMenu.Object, appSettings)).ParamName);
        Assert.Equal("greetingService", Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(mockAuth.Object, null!, mockMenu.Object, appSettings)).ParamName);
        Assert.Equal("navigationMenuService", Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(mockAuth.Object, mockGreeting.Object, null!, appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(mockAuth.Object, mockGreeting.Object, mockMenu.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenGreetsUserByDisplayName()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { DisplayName = "Bob" });
        mockGreeting.Setup(x => x.GetGreeting("Bob")).Returns("Good morning, Bob");
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Good morning, Bob", sut.Greeting);
    }

    [Fact]
    public async Task GivenNoUser_WhenInitialized_ThenUsesGenericGreeting()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        mockGreeting.Setup(x => x.GetGreeting(null)).Returns("Good morning");
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Good morning", sut.Greeting);
    }

    [Fact]
    public void GivenMenuItems_WhenGettingQuickLinks_ThenExcludesDashboard()
    {
        // Arrange
        mockMenu.Setup(x => x.GetMainItems()).Returns(
        [
            new NavigationItem { Text = "Dashboard", Href = string.Empty },
            new NavigationItem { Text = "Settings", Href = "settings" },
            new NavigationItem { Text = "Showcase", Href = "showcase" },
        ]);
        var sut = CreateSut();

        // Act
        var result = sut.QuickLinks;

        // Assert
        Assert.Equal(["Settings", "Showcase"], result.Select(i => i.Text));
        Assert.Same(appSettings, sut.AppSettings);
    }

    private DashboardViewModel CreateSut() => new(mockAuth.Object, mockGreeting.Object, mockMenu.Object, appSettings);
}
