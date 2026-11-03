using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Navigation;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NavigationMenuServiceTests
{
    private readonly Mock<IPageRegistry> mockPages = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => new NavigationMenuService(null!, mockPages.Object)).ParamName);
        Assert.Equal("pageRegistry", Assert.Throws<ArgumentNullException>(() => new NavigationMenuService([], null!)).ParamName);
    }

    [Fact]
    public void GivenItemsAddedInAnyOrder_WhenGetMainItems_ThenSortedByOrder()
    {
        // Arrange
        var settings = new NavigationItem { Text = "Settings", Order = 100 };
        var dashboard = new NavigationItem { Text = "Dashboard", Order = 0 };
        var files = new NavigationItem { Text = "Files", Order = 10 };
        var sut = new NavigationMenuService([settings, dashboard, files], mockPages.Object);

        // Act & Assert
        Assert.Equal([dashboard, files, settings], sut.GetMainItems());
    }

    [Fact]
    public void GivenItemForATurnedOffPage_WhenGetMainItems_ThenHidden()
    {
        // Arrange
        mockPages.Setup(x => x.IsEnabled("sync")).Returns(false);
        mockPages.Setup(x => x.IsEnabled("settings")).Returns(true);
        var sync = new NavigationItem { Text = "Sync", PageKey = "sync" };
        var settings = new NavigationItem { Text = "Settings", PageKey = "settings" };
        var appPage = new NavigationItem { Text = "Mine" };
        var sut = new NavigationMenuService([sync, settings, appPage], mockPages.Object);

        // Act & Assert
        Assert.Equal([settings, appPage], sut.GetMainItems());
    }
}
