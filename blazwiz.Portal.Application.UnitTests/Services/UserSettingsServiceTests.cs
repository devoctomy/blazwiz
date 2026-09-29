using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class UserSettingsServiceTests
{
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new UserSettingsService(null!));
        Assert.Equal("localStorageService", exception.ParamName);
    }

    [Fact]
    public async Task GivenStoredSettings_WhenGetSettingsAsync_ThenReturnsStoredSettings()
    {
        // Arrange
        var stored = new UserSettings { Theme = "dark" };
        mockLocalStorage.Setup(x => x.GetObjectAsync<UserSettings>(UserSettingsService.StorageKey)).ReturnsAsync(stored);
        var sut = new UserSettingsService(mockLocalStorage.Object);

        // Act
        var result = await sut.GetSettingsAsync();

        // Assert
        Assert.Same(stored, result);
    }

    [Fact]
    public async Task GivenNoStoredSettings_WhenGetSettingsAsync_ThenReturnsDefaults()
    {
        // Arrange
        mockLocalStorage.Setup(x => x.GetObjectAsync<UserSettings>(UserSettingsService.StorageKey)).ReturnsAsync((UserSettings?)null);
        var sut = new UserSettingsService(mockLocalStorage.Object);

        // Act
        var result = await sut.GetSettingsAsync();

        // Assert
        Assert.Null(result.Theme);
        Assert.False(result.SidebarCollapsed);
    }

    [Fact]
    public async Task GivenSettingsAlreadyLoaded_WhenGetSettingsAsync_ThenLocalStorageIsReadOnce()
    {
        // Arrange
        mockLocalStorage.Setup(x => x.GetObjectAsync<UserSettings>(UserSettingsService.StorageKey)).ReturnsAsync(new UserSettings());
        var sut = new UserSettingsService(mockLocalStorage.Object);

        // Act
        var first = await sut.GetSettingsAsync();
        var second = await sut.GetSettingsAsync();

        // Assert
        Assert.Same(first, second);
        mockLocalStorage.Verify(x => x.GetObjectAsync<UserSettings>(UserSettingsService.StorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenSettings_WhenSaveSettingsAsync_ThenStoresAndCachesSettings()
    {
        // Arrange
        var sut = new UserSettingsService(mockLocalStorage.Object);
        var settings = new UserSettings { Theme = "light", SidebarCollapsed = true };

        // Act
        await sut.SaveSettingsAsync(settings);
        var result = await sut.GetSettingsAsync();

        // Assert
        mockLocalStorage.Verify(x => x.SetObjectAsync(UserSettingsService.StorageKey, settings), Times.Once);
        mockLocalStorage.Verify(x => x.GetObjectAsync<UserSettings>(It.IsAny<string>()), Times.Never);
        Assert.Same(settings, result);
    }

    [Fact]
    public async Task GivenNullSettings_WhenSaveSettingsAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new UserSettingsService(mockLocalStorage.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveSettingsAsync(null!));
    }
}
