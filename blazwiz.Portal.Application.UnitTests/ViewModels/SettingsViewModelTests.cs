using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void GivenNullRegistry_WhenConstructing_ThenThrowsArgumentNullException()
    {
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
