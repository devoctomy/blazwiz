using blazwiz.Core.Time;

namespace blazwiz.Core.UnitTests.Time;

public class SystemClockTests
{
    [Fact]
    public void GivenSystemClock_WhenGettingUtcNow_ThenReturnsCurrentUtcTime()
    {
        // Arrange
        var sut = new SystemClock();
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = sut.UtcNow;

        // Assert
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(result, before, after);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void GivenSystemClock_WhenGettingNow_ThenReturnsCurrentLocalTime()
    {
        // Arrange
        var sut = new SystemClock();
        var before = DateTimeOffset.Now;

        // Act
        var result = sut.Now;

        // Assert
        var after = DateTimeOffset.Now;
        Assert.InRange(result, before, after);
    }
}
