using Devoctomy.Blazwiz.Browser.Services;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class NavigationServiceTests
{
    private readonly TestNavigationManager navigationManager = new();

    [Fact]
    public void GivenNullNavigationManager_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("navigationManager", Assert.Throws<ArgumentNullException>(() => new NavigationService(null!)).ParamName);
    }

    [Fact]
    public void GivenAUri_WhenNavigateTo_ThenTheNavigationManagerGoesThereWithoutForcingALoad()
    {
        // Arrange
        var sut = new NavigationService(navigationManager);

        // Act
        sut.NavigateTo("settings");

        // Assert
        var (uri, options) = Assert.Single(navigationManager.Navigations);
        Assert.Equal("settings", uri);
        Assert.False(options.ForceLoad);
        Assert.False(options.ReplaceHistoryEntry);
        Assert.Empty(navigationManager.Refreshes);
    }

    [Fact]
    public void GivenTheApp_WhenReload_ThenThePageIsRefreshedWithAForcedReload()
    {
        // Arrange
        var sut = new NavigationService(navigationManager);

        // Act
        sut.Reload();

        // Assert
        Assert.True(Assert.Single(navigationManager.Refreshes));
        Assert.Empty(navigationManager.Navigations);
    }

    /// <summary>
    /// Records navigations and refreshes instead of performing them.
    /// </summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.example/", "https://app.example/");
        }

        public List<(string Uri, NavigationOptions Options)> Navigations { get; } = [];

        public List<bool> Refreshes { get; } = [];

        public override void Refresh(bool forceReload = false)
        {
            Refreshes.Add(forceReload);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Navigations.Add((uri, options));
        }
    }
}
