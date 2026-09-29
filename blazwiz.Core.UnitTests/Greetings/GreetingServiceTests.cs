using blazwiz.Core.Greetings;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Greetings;

public class GreetingServiceTests
{
    private readonly Mock<ISystemClock> mockSystemClock = new();

    [Fact]
    public void GivenNullSystemClock_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new GreetingService(null!));
        Assert.Equal("systemClock", exception.ParamName);
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
        var sut = new GreetingService(mockSystemClock.Object);

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
        var sut = new GreetingService(mockSystemClock.Object);

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
        mockSystemClock
            .SetupGet(x => x.Now)
            .Returns(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero));
        var sut = new GreetingService(mockSystemClock.Object);

        // Act
        var result = sut.GetGreeting(name);

        // Assert
        Assert.Equal(expected, result);
    }
}
