using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class LocalStorageSyncEnabledStoreTests
{
    private readonly Mock<ILocalStorageService> mockStorage = new();

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageSyncEnabledStore(null!)).ParamName);
    }

    [Theory]
    [InlineData("True", false)]
    [InlineData(null, true)]
    [InlineData("rubbish", true)]
    public async Task GivenStoredValue_WhenGetAsync_ThenOnlyTrueTurnsSyncOff(string? stored, bool expected)
    {
        mockStorage.Setup(x => x.GetItemAsync(LocalStorageSyncEnabledStore.StorageKey)).ReturnsAsync(stored);

        Assert.Equal(expected, await new LocalStorageSyncEnabledStore(mockStorage.Object).GetAsync());
    }

    [Fact]
    public async Task GivenEarlierOfflineModeOn_WhenGetAsync_ThenSyncOffCarriedOverOnce()
    {
        // Arrange
        mockStorage.Setup(x => x.GetItemAsync(LocalStorageSyncEnabledStore.LegacyStorageKey)).ReturnsAsync("True");

        // Act
        var enabled = await new LocalStorageSyncEnabledStore(mockStorage.Object).GetAsync();

        // Assert
        Assert.False(enabled);
        mockStorage.Verify(x => x.SetItemAsync(LocalStorageSyncEnabledStore.StorageKey, "True"), Times.Once);
        mockStorage.Verify(x => x.RemoveItemAsync(LocalStorageSyncEnabledStore.LegacyStorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenOff_WhenSetAsync_ThenSaved()
    {
        await new LocalStorageSyncEnabledStore(mockStorage.Object).SetAsync(false);

        mockStorage.Verify(x => x.SetItemAsync(LocalStorageSyncEnabledStore.StorageKey, "True"), Times.Once);
    }

    [Fact]
    public async Task GivenOn_WhenSetAsync_ThenRemoved()
    {
        await new LocalStorageSyncEnabledStore(mockStorage.Object).SetAsync(true);

        mockStorage.Verify(x => x.RemoveItemAsync(LocalStorageSyncEnabledStore.StorageKey), Times.Once);
    }
}
