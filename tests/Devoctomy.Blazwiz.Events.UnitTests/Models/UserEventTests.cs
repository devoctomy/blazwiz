using System.Text.Json;
using Devoctomy.Blazwiz.Events.Models;

namespace Devoctomy.Blazwiz.Events.UnitTests.Models;

public class UserEventTests
{
    [Fact]
    public void GivenEvent_WhenSerialized_ThenItHasTheWireNamesAndNothingElse()
    {
        // Arrange
        var sut = new UserEvent
        {
            Type = UserEventTypes.SignedOutEverywhere,
            Reason = UserEventReasons.KeysRotated,
            Device = "Android-Edge-624834",
            At = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero),
            From = "abc",
        };

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(sut));

        // Assert
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["type", "reason", "device", "at", "from"], names);
        Assert.Equal("signed-out-everywhere", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("keys-rotated", json.RootElement.GetProperty("reason").GetString());
    }
}
