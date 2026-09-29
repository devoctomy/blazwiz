using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<IThemeService> mockTheme = new();

    [Fact]
    public void GivenNullThemeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!));
        Assert.Equal("themeService", exception.ParamName);
    }

    [Fact]
    public void GivenThemeService_WhenGettingCurrentTheme_ThenReturnsServiceTheme()
    {
        // Arrange
        mockTheme.SetupGet(x => x.CurrentTheme).Returns(ThemeNames.Dark);
        var sut = new SettingsViewModel(mockTheme.Object);

        // Act & Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
    }

    [Fact]
    public async Task GivenTheme_WhenSelectTheme_ThenSetsTheme()
    {
        // Arrange
        var sut = new SettingsViewModel(mockTheme.Object);

        // Act
        await sut.SelectThemeCommand.ExecuteAsync(ThemeNames.Dark);

        // Assert
        mockTheme.Verify(x => x.SetThemeAsync(ThemeNames.Dark), Times.Once);
    }

    [Fact]
    public void GivenThemeChanges_WhenThemeChangedRaised_ThenCurrentThemeChangeIsNotified()
    {
        // Arrange
        var sut = new SettingsViewModel(mockTheme.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Contains(nameof(SettingsViewModel.CurrentTheme), changed);
    }

    [Fact]
    public void GivenDisposed_WhenThemeChangedRaised_ThenNoLongerNotifies()
    {
        // Arrange
        var sut = new SettingsViewModel(mockTheme.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockTheme.Raise(x => x.ThemeChanged += null, EventArgs.Empty);

        // Assert
        Assert.Empty(changed);
    }
}
