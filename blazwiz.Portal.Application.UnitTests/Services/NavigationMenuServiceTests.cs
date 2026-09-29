using blazwiz.Portal.Application.Services;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NavigationMenuServiceTests
{
    [Fact]
    public void GivenService_WhenGetMainItems_ThenDashboardIsFirstAndExactMatch()
    {
        // Arrange
        var sut = new NavigationMenuService();

        // Act
        var items = sut.GetMainItems();

        // Assert
        var dashboard = items[0];
        Assert.Equal(string.Empty, dashboard.Href);
        Assert.True(dashboard.ExactMatch);
    }

    [Fact]
    public void GivenService_WhenGetMainItems_ThenEveryItemIsComplete()
    {
        // Arrange
        var sut = new NavigationMenuService();

        // Act
        var items = sut.GetMainItems();

        // Assert
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
            Assert.StartsWith("bi-", item.Icon);
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
        });
    }

    [Fact]
    public void GivenService_WhenGetMainItems_ThenHrefsAreUniqueAndRelative()
    {
        // Arrange
        var sut = new NavigationMenuService();

        // Act
        var items = sut.GetMainItems();

        // Assert
        Assert.Equal(items.Count, items.Select(i => i.Href).Distinct().Count());
        Assert.All(items, item => Assert.False(item.Href.StartsWith('/')));
    }
}
