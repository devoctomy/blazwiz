using blazwiz.Core.Sync.Storage;
using Microsoft.JSInterop;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Storage;

public class StorageDurabilityServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public StorageDurabilityServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == StorageDurabilityService.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new StorageDurabilityService(null!)).ParamName);
    }

    [Fact]
    public async Task GivenBrowserStatus_WhenGetStatusAsync_ThenReturnsIt()
    {
        // Arrange
        var status = new StorageDurabilityStatus { IsSupported = true, IsPersisted = true, UsageBytes = 2_100_000 };
        mockModule.Setup(x => x.InvokeAsync<StorageDurabilityStatus>("getStatus", It.IsAny<object?[]>())).ReturnsAsync(status);
        var sut = new StorageDurabilityService(mockJs.Object);

        // Act & Assert
        Assert.Same(status, await sut.GetStatusAsync());
    }

    [Fact]
    public async Task GivenBrowserGrants_WhenRequestPersistenceAsync_ThenTrueAndModuleImportedOnce()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("persist", It.IsAny<object?[]>())).ReturnsAsync(true);
        var sut = new StorageDurabilityService(mockJs.Object);

        // Act
        var first = await sut.RequestPersistenceAsync();
        await sut.RequestPersistenceAsync();

        // Assert
        Assert.True(first);
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }
}
