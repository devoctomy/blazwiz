using Devoctomy.Blazwiz.Auth.Migrations;
using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Migrations;

public class DataMigrationContextTests
{
    [Fact]
    public void GivenNullSession_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("session", Assert.Throws<ArgumentNullException>(() => new DataMigrationContext(null!)).ParamName);
    }

    [Fact]
    public void GivenASession_WhenConstructed_ThenItIsKept()
    {
        // Arrange
        var session = new UserSession { Username = "bob" };

        // Act
        var context = new DataMigrationContext(session);

        // Assert
        Assert.Same(session, context.Session);
    }
}
