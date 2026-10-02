using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class LocalStorageOfflineModeStoreTests
{
    private readonly Mock<ILocalStorageService> mockStorage = new();

    [Fact]
    public void GivenNullLocalStorage_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageOfflineModeStore(null!)).ParamName);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData(null, false)]
    [InlineData("rubbish", false)]
    public async Task GivenStoredValue_WhenGetAsync_ThenOnlyTrueIsOn(string? stored, bool expected)
    {
        mockStorage.Setup(x => x.GetItemAsync(LocalStorageOfflineModeStore.StorageKey)).ReturnsAsync(stored);

        Assert.Equal(expected, await new LocalStorageOfflineModeStore(mockStorage.Object).GetAsync());
    }

    [Fact]
    public async Task GivenOn_WhenSetAsync_ThenSaved()
    {
        await new LocalStorageOfflineModeStore(mockStorage.Object).SetAsync(true);

        mockStorage.Verify(x => x.SetItemAsync(LocalStorageOfflineModeStore.StorageKey, "True"), Times.Once);
    }

    [Fact]
    public async Task GivenOff_WhenSetAsync_ThenRemoved()
    {
        await new LocalStorageOfflineModeStore(mockStorage.Object).SetAsync(false);

        mockStorage.Verify(x => x.RemoveItemAsync(LocalStorageOfflineModeStore.StorageKey), Times.Once);
    }
}
