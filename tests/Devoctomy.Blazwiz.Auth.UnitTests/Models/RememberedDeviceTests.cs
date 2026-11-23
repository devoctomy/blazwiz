using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Models;

public class RememberedDeviceTests
{
    [Fact]
    public void GivenNewDevice_WhenReadingIt_ThenNoKeyNameOrLastSignIn_AndNotThisDevice()
    {
        // Act
        var device = new RememberedDevice();

        // Assert
        Assert.Equal(string.Empty, device.Key);
        Assert.Null(device.Name);
        Assert.Null(device.LastSignedInAt);
        Assert.False(device.IsThisDevice);
    }

    [Fact]
    public void GivenValues_WhenSettingThem_ThenTheyAreKept()
    {
        // Arrange
        var lastSignedIn = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

        // Act
        var device = new RememberedDevice { Key = "k1", Name = "Work laptop", LastSignedInAt = lastSignedIn, IsThisDevice = true };

        // Assert
        Assert.Equal("k1", device.Key);
        Assert.Equal("Work laptop", device.Name);
        Assert.Equal(lastSignedIn, device.LastSignedInAt);
        Assert.True(device.IsThisDevice);
    }
}
