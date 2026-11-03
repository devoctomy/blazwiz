using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class AppearanceSettingsViewModelTests
{
    private readonly Mock<IThemeService> mockTheme = new();

    [Fact]
    public void GivenNullThemeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("themeService", Assert.Throws<ArgumentNullException>(() => new AppearanceSettingsViewModel(null!)).ParamName);
    }

    [Fact]
    public void GivenThemeService_WhenGettingCurrentTheme_ThenReturnsServiceTheme()
    {
        // Arrange
        mockTheme.SetupGet(x => x.CurrentTheme).Returns(ThemeNames.Dark);
        var sut = new AppearanceSettingsViewModel(mockTheme.Object);

        // Act & Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
    }

    [Fact]
    public async Task GivenTheme_WhenSelectTheme_ThenSetsTheme()
    {
        // Arrange
        var sut = new AppearanceSettingsViewModel(mockTheme.Object);

        // Act
        await sut.SelectThemeCommand.ExecuteAsync(ThemeNames.Dark);

        // Assert
        mockTheme.Verify(x => x.SetThemeAsync(ThemeNames.Dark), Times.Once);
    }

    [Fact]
    public void GivenThemeChanges_WhenThemeChangedRaised_ThenCurrentThemeChangeIsNotified()
    {
        // Arrange
        var sut = new AppearanceSettingsViewModel(mockTheme.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(AppearanceSettingsViewModel.CurrentTheme), changed);
    }

    [Fact]
    public void GivenDisposed_WhenThemeChangedRaised_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = new AppearanceSettingsViewModel(mockTheme.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Empty(changed);
    }
}
