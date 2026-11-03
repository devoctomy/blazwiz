using Microsoft.Extensions.Time.Testing;
using MyApp.Portal.Application.Services;

namespace MyApp.Portal.Application.UnitTests.Services;

public class GreetingServiceTests
{
    private readonly FakeTimeProvider timeProvider = new();

    [Fact]
    public void GivenNullTimeProvider_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new GreetingService(null!));
        Assert.Equal("timeProvider", exception.ParamName);
    }

    [Theory]
    [InlineData(0, "Good evening")]
    [InlineData(4, "Good evening")]
    [InlineData(5, "Good morning")]
    [InlineData(11, "Good morning")]
    [InlineData(12, "Good afternoon")]
    [InlineData(17, "Good afternoon")]
    [InlineData(18, "Good evening")]
    [InlineData(23, "Good evening")]
    public void GivenHour_WhenGetGreetingPrefix_ThenReturnsExpectedPrefix(int hour, string expected)
    {
        // Arrange
        var sut = new GreetingService(timeProvider);

        // Act
        var result = sut.GetGreetingPrefix(hour);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void GivenInvalidHour_WhenGetGreetingPrefix_ThenThrowsArgumentOutOfRangeException(int hour)
    {
        // Arrange
        var sut = new GreetingService(timeProvider);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetGreetingPrefix(hour));
    }

    [Theory]
    [InlineData("Bob", "Good morning, Bob")]
    [InlineData("  Bob  ", "Good morning, Bob")]
    [InlineData("", "Good morning")]
    [InlineData("   ", "Good morning")]
    [InlineData(null, "Good morning")]
    public void GivenName_WhenGetGreeting_ThenReturnsGreetingForCurrentTime(string? name, string expected)
    {
        // Arrange
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero));
        var sut = new GreetingService(timeProvider);

        // Act
        var result = sut.GetGreeting(name);

        // Assert
        Assert.Equal(expected, result);
    }
}
