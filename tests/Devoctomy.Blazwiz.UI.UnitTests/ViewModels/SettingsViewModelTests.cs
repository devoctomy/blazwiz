using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void GivenNullRegistry_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("sectionRegistry", Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!)).ParamName);
    }

    [Fact]
    public void GivenRegisteredSections_WhenSections_ThenRegistryOrderReturned()
    {
        // Arrange
        var registry = new Mock<ISettingsSectionRegistry>();
        registry.Setup(x => x.GetSections()).Returns([typeof(string), typeof(int)]);
        var sut = new SettingsViewModel(registry.Object);

        // Act & Assert
        Assert.Equal([typeof(string), typeof(int)], sut.Sections);
    }
}
