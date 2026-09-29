using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class ThemeServiceTests
{
    private readonly Mock<IUserSettingsService> mockUserSettings = new();
    private readonly Mock<IBrowserInteropService> mockBrowserInterop = new();
    private readonly UserSettings settings = new();

    public ThemeServiceTests()
    {
        mockUserSettings.Setup(x => x.GetSettingsAsync()).ReturnsAsync(settings);
        mockBrowserInterop.Setup(x => x.GetPreferredThemeAsync()).ReturnsAsync(ThemeNames.Light);
    }

    [Fact]
    public void GivenNullUserSettingsService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ThemeService(null!, mockBrowserInterop.Object));
        Assert.Equal("userSettingsService", exception.ParamName);
    }

    [Fact]
    public void GivenNullBrowserInteropService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ThemeService(mockUserSettings.Object, null!));
        Assert.Equal("browserInteropService", exception.ParamName);
    }

    [Fact]
    public void GivenNewService_WhenCreated_ThenDefaultsToLight()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.Equal(ThemeNames.Light, sut.CurrentTheme);
        Assert.False(sut.IsDark);
    }

    [Fact]
    public async Task GivenSavedTheme_WhenInitializeAsync_ThenAppliesSavedTheme()
    {
        // Arrange
        settings.Theme = ThemeNames.Dark;
        var sut = CreateSut();

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
        Assert.True(sut.IsDark);
        mockBrowserInterop.Verify(x => x.SetThemeAsync(ThemeNames.Dark), Times.Once);
        mockBrowserInterop.Verify(x => x.GetPreferredThemeAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenNoSavedTheme_WhenInitializeAsync_ThenAppliesOperatingSystemPreference()
    {
        // Arrange
        mockBrowserInterop.Setup(x => x.GetPreferredThemeAsync()).ReturnsAsync(ThemeNames.Dark);
        var sut = CreateSut();

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
        mockBrowserInterop.Verify(x => x.SetThemeAsync(ThemeNames.Dark), Times.Once);
    }

    [Fact]
    public async Task GivenUnknownSavedTheme_WhenInitializeAsync_ThenFallsBackToLight()
    {
        // Arrange
        settings.Theme = "purple";
        var sut = CreateSut();

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(ThemeNames.Light, sut.CurrentTheme);
    }

    [Fact]
    public async Task GivenAlreadyInitialized_WhenInitializeAsync_ThenDoesNothing()
    {
        // Arrange
        var sut = CreateSut();
        await sut.InitializeAsync();

        // Act
        await sut.InitializeAsync();

        // Assert
        mockUserSettings.Verify(x => x.GetSettingsAsync(), Times.Once);
        mockBrowserInterop.Verify(x => x.SetThemeAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GivenInitialize_WhenCompleted_ThenRaisesThemeChanged()
    {
        // Arrange
        var sut = CreateSut();
        var raised = 0;
        sut.ThemeChanged += (_, _) => raised++;

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(ThemeNames.Light)]
    [InlineData(ThemeNames.Dark)]
    public async Task GivenValidTheme_WhenSetThemeAsync_ThenAppliesSavesAndRaisesThemeChanged(string theme)
    {
        // Arrange
        var sut = CreateSut();
        var raised = 0;
        sut.ThemeChanged += (_, _) => raised++;

        // Act
        await sut.SetThemeAsync(theme);

        // Assert
        Assert.Equal(theme, sut.CurrentTheme);
        Assert.Equal(theme, settings.Theme);
        Assert.Equal(1, raised);
        mockBrowserInterop.Verify(x => x.SetThemeAsync(theme), Times.Once);
        mockUserSettings.Verify(x => x.SaveSettingsAsync(settings), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidTheme_WhenSetThemeAsync_ThenThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => sut.SetThemeAsync("purple"));
        Assert.Equal("theme", exception.ParamName);
    }

    [Fact]
    public async Task GivenThemeSet_WhenInitializeAsync_ThenDoesNotOverrideSelection()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetThemeAsync(ThemeNames.Dark);

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(ThemeNames.Dark, sut.CurrentTheme);
        mockBrowserInterop.Verify(x => x.GetPreferredThemeAsync(), Times.Never);
    }

    [Theory]
    [InlineData(ThemeNames.Light, ThemeNames.Dark)]
    [InlineData(ThemeNames.Dark, ThemeNames.Light)]
    public async Task GivenCurrentTheme_WhenToggleThemeAsync_ThenSwitchesToOtherTheme(string current, string expected)
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetThemeAsync(current);

        // Act
        await sut.ToggleThemeAsync();

        // Assert
        Assert.Equal(expected, sut.CurrentTheme);
    }

    private ThemeService CreateSut() => new(mockUserSettings.Object, mockBrowserInterop.Object);
}
