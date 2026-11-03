using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class NavigationMenuServiceTests
{
    private readonly Mock<IPageRegistry> mockPages = new();
    private readonly TextOptions textOptions = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var factory = new OverridingStringLocalizerFactory(textOptions);

        // Act & Assert
        Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => new NavigationMenuService(null!, mockPages.Object, factory)).ParamName);
        Assert.Equal("pageRegistry", Assert.Throws<ArgumentNullException>(() => new NavigationMenuService([], null!, factory)).ParamName);
        Assert.Equal("localizerFactory", Assert.Throws<ArgumentNullException>(() => new NavigationMenuService([], mockPages.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenItemsAddedInAnyOrder_WhenGetMainItems_ThenSortedByOrder()
    {
        // Arrange
        var settings = new NavigationItem { Text = "Settings", Order = 100 };
        var dashboard = new NavigationItem { Text = "Dashboard", Order = 0 };
        var files = new NavigationItem { Text = "Files", Order = 10 };
        var sut = CreateSut(settings, dashboard, files);

        // Act
        var result = sut.GetMainItems();

        // Assert
        Assert.Equal([dashboard, files, settings], result);
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
        var sut = CreateSut(sync, settings, appPage);

        // Act
        var result = sut.GetMainItems();

        // Assert
        Assert.Equal([settings, appPage], result);
    }

    [Fact]
    public void GivenItemWithTextResource_WhenGetMainItems_ThenTextAndDescriptionComeFromIt()
    {
        // Arrange
        var item = new NavigationItem
        {
            Text = "Nav.Settings",
            Description = "Nav.Settings.Description",
            TextResource = typeof(UiText),
            Href = "settings",
            Icon = "bi-gear",
            Order = 7,
            PageKey = "settings",
            ExactMatch = true,
        };
        mockPages.Setup(x => x.IsEnabled("settings")).Returns(true);
        var sut = CreateSut(item);

        // Act
        var result = Assert.Single(sut.GetMainItems());

        // Assert
        Assert.Equal("Settings", result.Text);
        Assert.Equal("Appearance and application preferences.", result.Description);
        Assert.Null(result.TextResource);
        Assert.Equal(("settings", "bi-gear", 7, "settings", true), (result.Href, result.Icon, result.Order, result.PageKey, result.ExactMatch));
    }

    [Fact]
    public void GivenAppOverride_WhenGetMainItems_ThenTheOverrideIsShown()
    {
        // Arrange
        new TextBuilder(textOptions).Override("Nav.Settings", "Preferences");
        var sut = CreateSut(new NavigationItem { Text = "Nav.Settings", TextResource = typeof(UiText) });

        // Act
        var result = Assert.Single(sut.GetMainItems());

        // Assert
        Assert.Equal("Preferences", result.Text);
        Assert.Equal(string.Empty, result.Description);
    }

    private NavigationMenuService CreateSut(params NavigationItem[] items) =>
        new(items, mockPages.Object, new OverridingStringLocalizerFactory(textOptions));
}
