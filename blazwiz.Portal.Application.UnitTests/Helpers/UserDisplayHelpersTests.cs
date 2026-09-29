using blazwiz.Portal.Application.Helpers;

namespace blazwiz.Portal.Application.UnitTests.Helpers;

public class UserDisplayHelpersTests
{
    [Theory]
    [InlineData("Bob Hoskins", "BH")]
    [InlineData("bob", "B")]
    [InlineData("Mary Jane Watson", "MW")]
    [InlineData("jane.doe", "JD")]
    [InlineData("  alice  ", "A")]
    [InlineData(null, "?")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    [InlineData("...", "?")]
    public void GivenDisplayName_WhenGetInitials_ThenReturnsExpectedInitials(string? displayName, string expected)
    {
        // Act
        var result = UserDisplayHelpers.GetInitials(displayName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("bob.hoskins@example.com", "Bob Hoskins")]
    [InlineData("alice", "Alice")]
    [InlineData("mary_jane-watson", "Mary Jane Watson")]
    [InlineData("  bob@example.com  ", "Bob")]
    [InlineData("@example.com", "@example.com")]
    public void GivenUsername_WhenGetDisplayNameFromUsername_ThenReturnsFriendlyName(string username, string expected)
    {
        // Act
        var result = UserDisplayHelpers.GetDisplayNameFromUsername(username);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenBlankUsername_WhenGetDisplayNameFromUsername_ThenThrowsArgumentException(string? username)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => UserDisplayHelpers.GetDisplayNameFromUsername(username!));
    }
}
