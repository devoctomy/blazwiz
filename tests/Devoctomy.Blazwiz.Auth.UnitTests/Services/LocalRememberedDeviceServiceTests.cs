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
    public async Task GivenKey_WhenForgetAsync_ThenNothingFails_AndItIsRaisedWithTheKey()
    {
        // Arrange
        var sut = new LocalRememberedDeviceService();
        var forgotten = new List<string>();
        sut.Forgotten += (_, key) => forgotten.Add(key);

        // Act
        var exception = await Record.ExceptionAsync(() => sut.ForgetAsync("k1"));

        // Assert
        Assert.Null(exception);
        Assert.Equal(["k1"], forgotten);
    }

    [Fact]
    public async Task GivenNoListener_WhenForgetAsync_ThenNothingFails()
    {
        // Act
        var exception = await Record.ExceptionAsync(() => new LocalRememberedDeviceService().ForgetAsync("k1"));

        // Assert
        Assert.Null(exception);
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
