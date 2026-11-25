using Devoctomy.Blazwiz.Auth.Services;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class LocalRememberedDeviceServiceTests
{
    [Fact]
    public async Task GivenPlaceholder_WhenListAsync_ThenNone()
    {
        // Act
        var devices = await new LocalRememberedDeviceService().ListAsync();

        // Assert
        Assert.Empty(devices);
    }

    [Fact]
    public async Task GivenKey_WhenForgetAsync_ThenNothingHappens_AndNothingIsRaised()
    {
        // Arrange
        var sut = new LocalRememberedDeviceService();
        var raised = 0;
        EventHandler<string> handler = (_, _) => raised++;
        sut.Forgotten += handler;

        // Act
        var exception = await Record.ExceptionAsync(() => sut.ForgetAsync("k1"));
        sut.Forgotten -= handler;

        // Assert
        Assert.Null(exception);
        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GivenNoKey_WhenForgetAsync_ThenThrowsArgumentException(string? key)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new LocalRememberedDeviceService().ForgetAsync(key!));
    }
}
