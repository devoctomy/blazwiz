using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Connectivity;

public class LocalStorageSyncEnabledStoreTests
{
    private readonly Mock<ILocalStorageService> mockStorage = new();

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageSyncEnabledStore(null!)).ParamName);
    }

    [Theory]
    [InlineData("True", false)]
    [InlineData(null, true)]
    [InlineData("rubbish", true)]
    public async Task GivenStoredValue_WhenGetAsync_ThenOnlyTrueTurnsSyncOff(string? stored, bool expected)
    {
        // Arrange
        mockStorage.Setup(x => x.GetItemAsync(LocalStorageSyncEnabledStore.StorageKey)).ReturnsAsync(stored);

        // Act & Assert
        Assert.Equal(expected, await new LocalStorageSyncEnabledStore(mockStorage.Object).GetAsync());
    }

    [Fact]
    public async Task GivenOff_WhenSetAsync_ThenSaved()
    {
        // Act
        await new LocalStorageSyncEnabledStore(mockStorage.Object).SetAsync(false);

        // Assert
        mockStorage.Verify(x => x.SetItemAsync(LocalStorageSyncEnabledStore.StorageKey, "True"), Times.Once);
    }

    [Fact]
    public async Task GivenOn_WhenSetAsync_ThenRemoved()
    {
        // Act
        await new LocalStorageSyncEnabledStore(mockStorage.Object).SetAsync(true);

        // Assert
        mockStorage.Verify(x => x.RemoveItemAsync(LocalStorageSyncEnabledStore.StorageKey), Times.Once);
    }
}
