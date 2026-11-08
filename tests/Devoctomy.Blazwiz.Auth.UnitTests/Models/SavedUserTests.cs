using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Models;

public class SavedUserTests
{
    private readonly SavedUser sut = new() { Email = "bob@example.com" };

    [Theory]
    [InlineData("bob@example.com", null, true)]
    [InlineData("Bob@Example.COM", null, true)]
    [InlineData(" bob@example.com ", null, true)]
    [InlineData("bob", "bob@example.com", true)]
    [InlineData("bob", "BOB@example.com", true)]
    [InlineData("alice@example.com", null, false)]
    [InlineData("alice@example.com", "alice@example.com", false)]
    [InlineData(null, null, false)]
    public void GivenAUsernameAndEmail_WhenIsFor_ThenMatchesEitherWithoutRegardToCase(string? username, string? email, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, sut.IsFor(username, email));
    }
}
