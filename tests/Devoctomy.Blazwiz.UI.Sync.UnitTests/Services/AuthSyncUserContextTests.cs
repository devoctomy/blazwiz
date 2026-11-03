using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class AuthSyncUserContextTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullAuthService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new AuthSyncUserContext(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedIn_WhenGetUserIdAsync_ThenUsername()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });

        // Act & Assert
        Assert.Equal("bob@example.com", await new AuthSyncUserContext(mockAuth.Object).GetUserIdAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetUserIdAsync_ThenNull()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        // Act & Assert
        Assert.Null(await new AuthSyncUserContext(mockAuth.Object).GetUserIdAsync());
    }
}
